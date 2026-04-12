using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeExplode.Videos.ClosedCaptions;
using Microsoft.Extensions.Logging;


namespace YoutubeMusicPlayer.Infrastructure.External;

public class YoutubeService : IYoutubeService
{
    private readonly HttpClient _httpClient;
    private readonly YoutubeClient _youtube;
    private readonly IMemoryCache _cache;
    private readonly IDeezerService _deezerService;
    private readonly ILogger<YoutubeService> _logger;


    public YoutubeService(IMemoryCache cache, IDeezerService deezerService, ILogger<YoutubeService> logger)
    {
        _cache = cache;
        _deezerService = deezerService;
        _logger = logger;


        // ISOLATED HTTP CLIENT WITH MODERN HEADERS (Chrome 121)
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua", "\"Not A(Brand\";v=\"99\", \"Google Chrome\";v=\"121\", \"Chromium\";v=\"121\"");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
        
        _youtube = new YoutubeClient(_httpClient);
    }

    public async Task<string> GetAudioStreamUrlAsync(string videoUrl, string? title = null, string? artist = null, bool isPremium = false)
    {
        string youtubeId = videoUrl;
        if (videoUrl.Contains("v=")) {
            var parts = videoUrl.Split("v=");
            if (parts.Length > 1) {
                youtubeId = parts[1].Split("&").First();
            }
        } else if (videoUrl.Contains("youtu.be/")) {
            youtubeId = videoUrl.Split("youtu.be/").Last().Split("?").First();
        }

        string cacheKey = $"stream_v5_{youtubeId}";
        if (_cache.TryGetValue(cacheKey, out string? cachedUrl)) return cachedUrl!;

        try 
        {
            return await GetUrlInternalAsync(youtubeId, cacheKey, isPremium);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YoutubeService] Lỗi trích xuất chính cho {youtubeId}: {ex.Message}");
            
            try 
            {
                // LOGIC PHỤC HỒI: Tìm kiếm phiên bản ổn định hơn (audio-only, lyrics, official audio)
                string fallbackQuery = !string.IsNullOrEmpty(title) 
                    ? $"{title} {artist} official audio" 
                    : "music high quality audio";
                
                var searchResults = await _youtube.Search.GetVideosAsync(fallbackQuery).CollectAsync(3); 

                foreach(var fallbackVideo in searchResults)
                {
                    if (fallbackVideo.Id == youtubeId) continue;
                    
                    try {
                        Console.WriteLine($"[YoutubeService] Đang thử nguồn dự phòng: {fallbackVideo.Title} ({fallbackVideo.Id})");
                        return await GetUrlInternalAsync(fallbackVideo.Id, cacheKey, isPremium);
                    } catch { continue; }
                }
            }
            catch (Exception recoveryEx)
            {
                Console.WriteLine($"[YoutubeService] Recovery failed: {recoveryEx.Message}");
            }

            throw new Exception($"Không thể trích xuất âm thanh cho video này. Lỗi: {ex.Message}");
        }
    }

    private async Task<string> GetUrlInternalAsync(string youtubeId, string cacheKey, bool isPremium)
    {
        _logger.LogInformation("[YoutubeService] Resolving manifest for: {VideoId}", youtubeId);
        
        // Use exponential backoff for manifest fetching if needed, but for now simple try/catch
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(youtubeId);
        
        // Priority: M4A Audio-only (best bit rate) -> Other Audio-only -> Best available
        var audioStreams = manifest.GetAudioOnlyStreams().ToList();
        
        IStreamInfo? selectedStream = audioStreams
            .OrderByDescending(s => s.Container.Name == "m4a") // Favor m4a for stability in browsers
            .ThenByDescending(s => s.Bitrate)
            .FirstOrDefault();
        
        if (selectedStream == null)
        {
            _logger.LogWarning("[YoutubeService] No audio-only streams found for {VideoId}. Falling back to muxed.", youtubeId);
            selectedStream = manifest.GetMuxedStreams().OrderByDescending(s => s.VideoQuality).FirstOrDefault();
        }

        if (selectedStream == null) throw new Exception("Không tìm thấy luồng âm thanh nào khả dụng.");

        var url = selectedStream.Url;
        _logger.LogInformation("[YoutubeService] Selected stream: {Container} ({Bitrate}) for {VideoId}", selectedStream.Container, selectedStream.Bitrate, youtubeId);
        
        // Cache link (Standardized to 30 mins for safety against signature expiry)
        _cache.Set(cacheKey, url, TimeSpan.FromMinutes(30));
        return url;
    }

    public async Task<YoutubeVideoDetails> GetVideoDetailsAsync(string videoUrl)
    {
        return await GetVideoDetailsInternalAsync(videoUrl, true);
    }

    public async Task<YoutubeVideoDetails> GetBasicVideoDetailsAsync(string videoUrl)
    {
        return await GetVideoDetailsInternalAsync(videoUrl, false);
    }

    private async Task<YoutubeVideoDetails> GetVideoDetailsInternalAsync(string videoUrl, bool enrich)
    {
        string cacheType = enrich ? "details" : "basic";
        string cacheKey = $"{cacheType}_{videoUrl}";
        
        if (_cache.TryGetValue(cacheKey, out YoutubeVideoDetails? cachedDetails))
        {
            return cachedDetails!;
        }

        var video = await _youtube.Videos.GetAsync(videoUrl);
        
        string authorAvatarUrl = string.Empty;
        try 
        {
            var channel = await _youtube.Channels.GetAsync(video.Author.ChannelId);
            authorAvatarUrl = channel.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url ?? "";
        }
        catch { }

        var hashtags = ExtractHashtags(video.Description);
        var genre = GuessGenre(video.Title, new List<string>());

        var details = new YoutubeVideoDetails
        {
            Title = video.Title,
            AuthorName = video.Author.ChannelTitle,
            AuthorChannelId = video.Author.ChannelId,
            AuthorAvatarUrl = authorAvatarUrl,
            YoutubeVideoId = video.Id,
            ThumbnailUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url,
            Duration = video.Duration,
            ViewCount = video.Engagement.ViewCount,
            Hashtags = hashtags,
            Genre = genre
        };

        if (enrich)
        {
            details = await EnrichVideoDetailsAsync(details);
        }
        else 
        {
            // Basic enrichment: Parse artists and titles but skip external API calls (Deezer/AI Tags)
            var parsed = ParseTitle(details.Title, details.AuthorName);
            details.CleanedTitle = CleanTitleString(parsed.Song);
            details.CleanedArtist = NormalizeArtist(parsed.Artist);
            details.TrackType = DetectTrackType(details.Title);
        }

        _cache.Set(cacheKey, details, TimeSpan.FromHours(enrich ? 1 : 24));
        return details;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetChannelVideosAsync(string channelId)
    {
        string cacheKey = $"channel_videos_all_{channelId}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached))
        {
            return cached!;
        }

        var allVideos = await _youtube.Channels.GetUploadsAsync(channelId).ToListAsync();
        
        var musicKeywords = new[] { "official", "music", "video", "audio", "lyric", "remix", "track", "album", "live", "mv" };
        var excludeKeywords = new[] { "vlog", "podcast", "interview", "challenge", "story", "review", "reaction" };

        var filteredResultsRaw = allVideos
            .Where(v => {
                var title = v.Title.ToLower();
                bool isMusic = musicKeywords.Any(k => title.Contains(k)) || v.Duration > TimeSpan.FromMinutes(1);
                bool isExcluded = excludeKeywords.Any(k => title.Contains(k));
                return isMusic && !isExcluded;
            })
            .Take(50);

        var tasks = filteredResultsRaw.Select(v => MapToFullDetailsAsync(v));

        var filteredResults = (await Task.WhenAll(tasks)).ToList();

        _cache.Set(cacheKey, filteredResults, TimeSpan.FromHours(12));
        return filteredResults;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> SearchVideosAsync(string query, int limit = 30, bool searchCompilations = false)
    {
        string cacheKey = $"search_v8_{query}_{limit}_{searchCompilations}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        // 1. Optimize query for MUSIC only
        string optimizedQuery = searchCompilations ? query : (query.EndsWith("music") || query.EndsWith("songs") ? query : $"{query} official music audio");
        
        var results = await _youtube.Search.GetVideosAsync(optimizedQuery).CollectAsync(Math.Max(limit * 2, 50)); 
        var filtered = results.Where(v => IsLikelyMusic(v, searchCompilations)).ToList();

        var tasks = filtered
            .Take(limit) 
            .Select(v => MapToFullDetailsAsync(v));

        var searchResponses = (await Task.WhenAll(tasks))
            .ToList();
        
        // Final optimization: Sort by most relevant (likely official/audio version)
        var sorted = searchResponses.OrderByDescending(v => v.TrackType == "Official" ? 2 : 1).ToList();

        _cache.Set(cacheKey, sorted, TimeSpan.FromMinutes(10));
        return sorted;
    }

    public async Task<IEnumerable<YoutubeAlbumDetails>> SearchPlaylistsAsync(string query, int limit = 5)
    {
        string optimizedQuery = $"{query} album full playlist";
        var playlists = await _youtube.Search.GetPlaylistsAsync(optimizedQuery).CollectAsync(limit);
        
        return playlists.Select(p => new YoutubeAlbumDetails
        {
            Title = p.Title,
            ArtistName = p.Author?.ChannelTitle ?? "Various Artists",
            YoutubePlaylistId = p.Id,
            ThumbnailUrl = p.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url,
            Type = "Album"
        });
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetPlaylistVideosAsync(string playlistId)
    {
        var videos = await _youtube.Playlists.GetVideosAsync(playlistId).CollectAsync(50);
        return videos.Select(v => new YoutubeVideoDetails
        {
            Title = v.Title,
            AuthorName = v.Author.ChannelTitle,
            YoutubeVideoId = v.Id,
            ThumbnailUrl = v.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url,
            Duration = v.Duration,
            TrackType = DetectTrackType(v.Title)
        });
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetTrendingMusicAsync(int limit = 15, bool forceRefresh = false)
    {
        string cacheKey = $"trending_music_v10_{limit}";

        try {
            var now = DateTime.UtcNow;
            var monthYear = now.ToString("MMMM yyyy");
            var queries = new List<string> {
                $"Nhạc Việt mới nhất {monthYear}",
                $"YouTube Music Trends Global {now.Year}",
                "V-Pop Top Trending official music",
                $"BXH Nhạc Trẻ {monthYear} hot nhất",
                "Nonstop VinaHouse 2026",
                "Lofi Việt nhẹ nhàng chill",
                "Rap Việt mới nhất underground",
                "Nhạc US-UK Chart hits 2026"
            }.OrderBy(_ => Random.Shared.Next()).Take(3).ToList();

            // Fetch exactly what's needed plus a small buffer for duplicates
            var fetchLimit = limit + 5;
            var tasks = queries.Select(q => SearchVideosAsync(q, fetchLimit));
            var resultsArray = await Task.WhenAll(tasks);
            
            var allResults = resultsArray.SelectMany(x => x).ToList();

            var uniqueResults = allResults
                .GroupBy(v => v.YoutubeVideoId)
                .Select(g => g.First())
                .Where(IsMusic)
                .OrderBy(_ => Random.Shared.Next())
                .Take(limit)
                .ToList();

            _cache.Set(cacheKey, uniqueResults, TimeSpan.FromHours(4));
            return uniqueResults;
        } catch { return Enumerable.Empty<YoutubeVideoDetails>(); }
    }

    private async Task<YoutubeVideoDetails> MapToFullDetailsAsync(IVideo v)
    {
        // NO Deezer enrichment during search to avoid 429
        var details = new YoutubeVideoDetails
        {
            Title = v.Title,
            AuthorName = v.Author.ChannelTitle,
            AuthorChannelId = v.Author.ChannelId,
            YoutubeVideoId = v.Id,
            ThumbnailUrl = v.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url,
            Duration = v.Duration,
            TrackType = DetectTrackType(v.Title),
            Genre = GuessGenre(v.Title, new List<string>())
        };
        return details;
    }

    private (string Artist, string Song) ParseTitle(string title, string author)
    {
        var splitters = new[] { " - ", " | ", " – ", ": ", " by " };
        foreach (var s in splitters)
        {
            if (title.Contains(s))
            {
                var parts = title.Split(new[] { s }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) return (parts[0].Trim(), parts[1].Trim());
            }
        }
        return (author, title);
    }

    public bool IsTooSimilar(string s1, string s2, double threshold = 0.5)
    {
        string Clean(string s) => System.Text.RegularExpressions.Regex.Replace(s.ToLower(), @"\(.*?\)|\[.*?\]|official|music|video|audio|lyrics|mv| - topic|vevo", "").Trim();
        
        var t1 = Clean(s1);
        var t2 = Clean(s2);

        if (t1 == t2) return true;

        var tokens1 = t1.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 2).ToList();
        var tokens2 = t2.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 2).ToList();

        if (!tokens1.Any() || !tokens2.Any()) return false;

        var common = tokens1.Intersect(tokens2).Count();
        double similarity = (double)common / Math.Max(tokens1.Count, tokens2.Count);
        
        return similarity > threshold; 
    }
    public bool IsMusic(YoutubeVideoDetails details)
    {
        return IsLikelyMusicCore(details.Title, details.AuthorName, details.Duration) && !IsCompilation(details) && !IsKaraoke(details);
    }

    public bool IsCompilation(YoutubeVideoDetails details)
    {
        var title = details.Title.ToLower();
        
        // Comprehensive list of ranking/countdown/collection keywords
        var rankingKeywords = new[] { 
            "top 10", "top 20", "top 50", "top 100", 
            "bxh", "bảng xếp hạng", "chart", "ranking", "countdown",
            "top bài hát", "top music", "top hits", "tổng hợp",
            "full album", "nonstop", "mix", "collection", "best of",
            "tuyển tập", "playlist", "tổng hợp nhạc", "nhạc tuyển tập"
        };

        bool isRankingOrComp = rankingKeywords.Any(k => title.Contains(k));
                               
        // If it's a ranking or longer than 7 mins, it's a compilation/non-single-track
        if (details.Duration.HasValue && details.Duration.Value.TotalMinutes >= 7.0) return true;
        
        return isRankingOrComp;
    }

    public bool IsKaraoke(YoutubeVideoDetails details)
    {
        var title = details.Title.ToLower();
        return title.Contains("karaoke") || title.Contains("beat") || title.Contains("tách lời") || title.Contains("không lời");
    }

    private bool IsLikelyMusic(YoutubeExplode.Search.VideoSearchResult v, bool searchCompilations = false)
    {
        return IsLikelyMusicCore(v.Title, v.Author.ChannelTitle, v.Duration, searchCompilations);
    }

    private bool IsLikelyMusicCore(string title, string author, TimeSpan? duration, bool searchCompilations = false)
    {
        var t = title.ToLower();
        
        var rankingKeywords = new[] { 
            "top 10", "top 20", "top 50", "top 100", 
            "bxh", "bảng xếp hạng", "chart", "ranking", "countdown",
            "top bài hát", "top music", "top hits", "tổng hợp",
            "full album", "nonstop", "mix", "collection", "best of",
            "tuyển tập", "playlist", "tổng hợp nhạc", "nhạc tuyển tập"
        };

        if (searchCompilations)
        {
            // For explicitly searching albums/compilations, we allow them
            return true;
        }

        // For regular discovery/search, strictly exclude non-single tracks
        // INCREASED LIMIT: Pop MVs with long intros can be up to 10 mins (e.g. Son Tung M-TP, Jack)
        if (duration.HasValue && duration.Value.TotalMinutes > 10.0 && !t.Contains("official") && !t.Contains("mv")) return false; 
        
        if (rankingKeywords.Any(k => t.Contains(k))) 
        {
            // Rare exception: if the title is just "Top of the World" (short and common song names)
            // But usually countdowns have "Top [Number]" or "BXH"
            if (t.Contains("top") && t.Length < 25) return true;
            return false;
        }
        
        if (t.Contains("karaoke") || t.Contains("beat") || t.Contains("không lời") || t.Contains("tách lời")) return false;
        
        return true;
    }


    private string GuessGenre(string title, List<string> tags)
    {
        title = title.ToLower();
        var allMarkers = tags.Select(t => t.ToLower()).ToList();
        allMarkers.Add(title);

        // Priority Logic: Check markers first
        if (allMarkers.Any(m => m.Contains("kpop") || m.Contains("k-pop") || m.Contains("korean"))) return "K-Pop";
        if (allMarkers.Any(m => m.Contains("jpop") || m.Contains("j-pop") || m.Contains("japanese") || m.Contains("anime"))) return "J-Pop/Anime";
        if (allMarkers.Any(m => m.Contains("remix") || m.Contains("vinahouse") || m.Contains("nonstop") || m.Contains("ncs") || m.Contains("edm"))) return "Remix";
        if (allMarkers.Any(m => m.Contains("lofi") || m.Contains("chill") || m.Contains("sleep") || m.Contains("relax"))) return "Lofi/Chill";
        if (allMarkers.Any(m => m.Contains("rap") || m.Contains("hiphop") || m.Contains("hip hop") || m.Contains("trap"))) return "Rap/Hip-Hop";
        if (allMarkers.Any(m => m.Contains("classic") || m.Contains("classical") || m.Contains("orchestra") || m.Contains("piano") || m.Contains("instrumental"))) return "Nhạc Classic";
        if (allMarkers.Any(m => m.Contains("ballad") || m.Contains("buồn") || m.Contains("tâm trạng"))) return "Ballad";
        if (allMarkers.Any(m => m.Contains("nhạc trẻ") || m.Contains("vpop") || m.Contains("v-pop") || m.Contains("việt"))) return "Nhạc trẻ";
        if (allMarkers.Any(m => m.Contains("us-uk") || m.Contains("usuk") || m.Contains("vevo") || m.Contains("pop"))) return "US-UK";

        return "Nhạc trẻ"; // Default fallback to Vietnamese popular category
    }


    private List<string> ExtractHashtags(string description)
    {
        if (string.IsNullOrEmpty(description)) return new List<string>();
        var matches = System.Text.RegularExpressions.Regex.Matches(description, @"#\w+");
        return matches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).ToList();
    }

    // --- DATA ENRICHMENT LAYER ---
    private async Task<YoutubeVideoDetails> EnrichVideoDetailsAsync(YoutubeVideoDetails details)
    {
        var parsed = ParseTitle(details.Title, details.AuthorName);
        details.CleanedTitle = CleanTitleString(parsed.Song);
        details.CleanedArtist = NormalizeArtist(parsed.Artist);
        details.TrackType = DetectTrackType(details.Title);

        // SYNC WITH DEEZER (Safe enrichment)
        try {
            if (IsMusic(details))
                await TryEnrichWithDeezerAsync(details);
        } catch (Exception ex) {
            Console.WriteLine($"[YoutubeService] Metadata enrichment warning: {ex.Message}");
        }

        details.Tags = ExtractAI_Tags(details); 

        return details;
    }

    private async Task TryEnrichWithDeezerAsync(YoutubeVideoDetails details)
    {
        try
        {
            var deezerTrack = await _deezerService.SearchTrackAsync(details.CleanedTitle, details.CleanedArtist);
            if (deezerTrack != null)
            {
                details.CleanedArtist = deezerTrack.ArtistName;
                details.CleanedTitle = deezerTrack.TrackName;
                if (deezerTrack.Genres.Any())
                {
                    details.Genre = NormalizeGenre(deezerTrack.Genres.First());
                }
            }
        }
        catch { }
    }

    private string NormalizeGenre(string g)
    {
        g = g.ToLower();
        if (g.Contains("pop") || g.Contains("v-pop")) return "Nhạc Pop";
        if (g.Contains("remix") || g.Contains("house") || g.Contains("edm")) return "Remix";
        if (g.Contains("ballad")) return "Ballad";
        if (g.Contains("k-pop") || g.Contains("kpop")) return "K-Pop";
        if (g.Contains("classic")) return "Nhạc Classic";
        return "US-UK";
    }

    private string CleanTitleString(string title)
    {
        return System.Text.RegularExpressions.Regex.Replace(title, @"\(.*?\)|\[.*?\]|official|music|video|audio|lyrics|mv", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
    }

    private string NormalizeArtist(string artist)
    {
        string[] splitters = new[] { " ft.", " feat.", " x ", " & ", ",", " ft ", " feat " };
        foreach (var s in splitters)
        {
            var parts = System.Text.RegularExpressions.Regex.Split(artist, s, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (parts.Length > 1) return parts[0].Trim();
        }
        return artist.Trim();
    }

    private string DetectTrackType(string title)
    {
        var t = title.ToLower();
        if (t.Contains("karaoke") || t.Contains("beat")) return TrackTypes.Karaoke;
        if (t.Contains("tổng hợp") || t.Contains("full album") || t.Contains("nonstop") || t.Contains("collection")) return TrackTypes.Compilation;
        if (t.Contains("remix") || t.Contains("mix")) return TrackTypes.Remix;
        if (t.Contains("live") || t.Contains("concert")) return TrackTypes.Live;
        if (t.Contains("cover")) return TrackTypes.Cover;
        if (t.Contains("lyric")) return TrackTypes.Lyrics;
        if (t.Contains("acoustic")) return TrackTypes.Acoustic;
        return TrackTypes.Official;
    }

    private List<string> ExtractAI_Tags(YoutubeVideoDetails v)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var t = v.Title.ToLower();
        var a = v.AuthorName.ToLower();
        
        // Duration-based tags
        if (v.Duration.HasValue)
        {
            if (v.Duration.Value.TotalMinutes < 3) tags.Add("fast");
            if (v.Duration.Value.TotalMinutes >= 5) tags.Add("chill/long");
        }
        
        // Keyword-based sentiment/vibe
        if (t.Contains("remix") || t.Contains("dj") || t.Contains("vinahouse")) { tags.Add("energetic"); tags.Add("electronic"); }
        if (t.Contains("acoustic") || t.Contains("guitar") || t.Contains("piano")) { tags.Add("chill"); tags.Add("instrumental"); }
        if (t.Contains("live")) tags.Add("vocal");
        if (t.Contains("lofi") || t.Contains("chill")) tags.Add("lofi");
        if (t.Contains("sad") || t.Contains("buồn")) tags.Add("sad");
        
        // Channel-based tags
        if (a.Contains("vevo") || a.Contains("topic")) tags.Add("official");
        
        if (v.Genre != "General") tags.Add(v.Genre);

        return tags.ToList();
    }
    public async Task<List<CaptionTrackDto>> GetAvailableCaptionTracksAsync(string videoId)
    {
        try
        {
            var trackManifest = await _youtube.Videos.ClosedCaptions.GetManifestAsync(videoId);
            if (trackManifest == null) return new List<CaptionTrackDto>();

            // The user wants primary ones: Vietnamese, English, and Auto-generated
            return trackManifest.Tracks
                .Where(t => t.Language.Code == "vi" || t.Language.Code == "en" || t.IsAutoGenerated)
                .Select(t => new CaptionTrackDto
                {
                    LanguageCode = t.Language.Code,
                    LanguageName = t.Language.Name,
                    IsAutoGenerated = t.IsAutoGenerated
                })
                .GroupBy(x => x.LanguageCode) // Avoid duplicates if any
                .Select(g => g.First())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YoutubeService] Error listing captions for {VideoId}", videoId);
            return new List<CaptionTrackDto>();
        }
    }

    public async Task<Application.Interfaces.ClosedCaptionTrack?> GetClosedCaptionsAsync(string videoId, string? langCode = null)
    {
        try
        {
            var trackManifest = await _youtube.Videos.ClosedCaptions.GetManifestAsync(videoId);
            if (trackManifest == null || !trackManifest.Tracks.Any()) 
            {
                _logger.LogWarning("[YoutubeService] No caption tracks found for {VideoId}", videoId);
                return null;
            }
            
            ClosedCaptionTrackInfo? trackInfo = null;

            if (!string.IsNullOrEmpty(langCode))
            {
                trackInfo = trackManifest.TryGetByLanguage(langCode);
            }

            if (trackInfo == null)
            {
                // Fallback to priority logic
                trackInfo = trackManifest.TryGetByLanguage("vi") 
                             ?? trackManifest.TryGetByLanguage("en")
                             ?? trackManifest.Tracks.FirstOrDefault(t => t.IsAutoGenerated)
                             ?? trackManifest.Tracks.FirstOrDefault();
            }

            if (trackInfo == null) return null;

            _logger.LogInformation("[YoutubeService] Selected track: {Language} (Auto: {Auto}) for {VideoId}", trackInfo.Language.Name, trackInfo.IsAutoGenerated, videoId);

            var track = await _youtube.Videos.ClosedCaptions.GetAsync(trackInfo);
            
            var lines = track.Captions.Select(c => new TimedLyricLine
            {
                StartTime = c.Offset.TotalSeconds,
                Duration = c.Duration.TotalSeconds,
                Text = c.Text
            }).ToList();

            var lyrics = string.Join("\n", lines.Select(l => l.Text));
            
            return new Application.Interfaces.ClosedCaptionTrack
            {
                Text = lyrics,
                Language = trackInfo.Language.Name,
                IsAutoGenerated = trackInfo.IsAutoGenerated,
                Lines = lines
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YoutubeService] Error fetching captions for {VideoId}", videoId);
            return null;
        }
    }
}

