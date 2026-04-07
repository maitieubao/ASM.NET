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

namespace YoutubeMusicPlayer.Infrastructure.External;

public class YoutubeService : IYoutubeService
{
    private readonly HttpClient _httpClient;
    private readonly YoutubeClient _youtube;
    private readonly IMemoryCache _cache;
    private readonly IDeezerService _deezerService;

    public YoutubeService(IMemoryCache cache, IDeezerService deezerService)
    {
        _cache = cache;
        _deezerService = deezerService;

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
        // Sử dụng GetManifestAsync với retry logic nội bộ nếu cần
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(youtubeId);
        
        // Ưu tiên Audio-only streams với Bitrate tốt nhất
        var audioStreams = manifest.GetAudioOnlyStreams();
        
        // Lọc bỏ các stream có thể gây lỗi hoặc không phù hợp
        var streamOptions = audioStreams
            .OrderByDescending(s => s.Bitrate)
            .ToList();
        
        if (!streamOptions.Any()) throw new Exception("Không tìm thấy luồng âm thanh nào khả dụng.");

        // Lựa chọn chất lượng stream dựa trên gói thành viên
        IStreamInfo selectedStream = streamOptions.First(); 
        
        if (!isPremium && streamOptions.Count > 1)
        {
            // Đối với người dùng free, chọn stream ở giữa để tiết kiệm băng thông và tăng độ ổn định
            int index = Math.Min(1, streamOptions.Count - 1); 
            selectedStream = streamOptions[index];
        }

        var url = selectedStream.Url;
        Console.WriteLine($"[YoutubeService] Thành công! ID: {youtubeId}, Bitrate: {selectedStream.Bitrate}, Size: {selectedStream.Size}");
        
        // Cache link trong 60 phút (link YouTube thường hết hạn sau 6h, nhưng 60p là an toàn nhất)
        _cache.Set(cacheKey, url, TimeSpan.FromHours(1));
        return url;
    }

    public async Task<YoutubeVideoDetails> GetVideoDetailsAsync(string videoUrl)
    {
        string cacheKey = $"details_{videoUrl}";
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

        details = await EnrichVideoDetailsAsync(details);

        _cache.Set(cacheKey, details, TimeSpan.FromHours(1));
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

    private bool IsTooSimilar(string s1, string s2, double threshold = 0.5)
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
        bool hasCompKeywords = title.Contains("tổng hợp") || title.Contains("full album") || 
                               title.Contains("nonstop") || title.Contains("mix") || 
                               title.Contains("collection") || title.Contains("best of") ||
                               title.Contains("tuyển tập") || title.Contains("playlist") ||
                               title.Contains("tổng hợp nhạc") || title.Contains("nhạc tuyển tập");
                               
        if (details.Duration.HasValue && details.Duration.Value.TotalMinutes >= 7.0) return true;
        
        return hasCompKeywords;
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
        bool hasCompKeywords = t.Contains("tổng hợp") || t.Contains("full album") || 
                               t.Contains("nonstop") || t.Contains("mix") || 
                               t.Contains("collection") || t.Contains("best of") ||
                               t.Contains("tuyển tập") || t.Contains("playlist") ||
                               t.Contains("tổng hợp nhạc") || t.Contains("nhạc tuyển tập");

        if (searchCompilations)
        {
            // For compilations, we WANT long videos or compilation keywords
            return true;
        }

        // For regular sections (Discovery, Charts), we want single tracks.
        if (duration.HasValue && duration.Value.TotalMinutes > 7.0) return false; 
        
        // RELIABILITY GATE: If keywords say it's a compilation, it's NOT a single track
        if (hasCompKeywords) return false;
        
        if (t.Contains("karaoke") || t.Contains("beat") || t.Contains("không lời")) return false;
        
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
}

