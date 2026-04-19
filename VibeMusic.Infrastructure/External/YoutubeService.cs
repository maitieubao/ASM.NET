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

        // OPTIMIZED HTTP HANDLER FOR REDUCED LATENCY
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2), // Re-establish connections to pick up DNS changes
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromSeconds(5) // Fast fail on slow handshake
        };

        _httpClient = new HttpClient(handler);
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
        
        // Cache link (5 hours - YouTube URL valid for ~6 hours)
        _cache.Set(cacheKey, url, TimeSpan.FromHours(5));
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
        if (enrich)
        {
            try 
            {
                var channel = await _youtube.Channels.GetAsync(video.Author.ChannelId);
                authorAvatarUrl = channel.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url ?? "";
            }
            catch { }
        }

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
        return await SearchVideosInternalAsync(query, limit, searchCompilations, 0);
    }

    private async Task<IEnumerable<YoutubeVideoDetails>> SearchVideosInternalAsync(string query, int limit, bool searchCompilations, int depth)
    {
        string cacheKey = $"search_v11_{query}_{limit}_{searchCompilations}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        _logger.LogInformation("[YoutubeService] Searching YouTube with query: {Query}", query);
        
        // 1. Fetch results from YouTube using ORIGINAL query
        var searchResults = await _youtube.Search.GetVideosAsync(query).CollectAsync(Math.Max(limit * 3, 60)); 
        
        // 2. Filter & Map to details
        var rawList = searchResults.ToList();
        var detailsTasks = new List<Task<YoutubeVideoDetails>>();
        
        for (int i = 0; i < rawList.Count; i++) {
            var v = rawList[i];
            if (IsLikelyMusic(v, searchCompilations)) {
                detailsTasks.Add(MapToFullDetailsAsync(v, false));
            }
        }

        var detailsList = (await Task.WhenAll(detailsTasks)).ToList();

        // 3. SCORING ENGINE
        var scoredList = detailsList.Select((v, index) => {
            double currentScore = (detailsList.Count - index) * 10.0;
            
            string authorLower = v.AuthorName.ToLower();
            string titleLower = v.Title.ToLower();

            // Authority Bonus
            if (authorLower.Contains("vevo") || authorLower.Contains("- topic") || authorLower.Contains("official")) 
                currentScore += 500;
            
            // Type Bonus
            if (v.TrackType == TrackTypes.OfficialMV) currentScore += 300;
            else if (v.TrackType == TrackTypes.OfficialAudio) currentScore += 200;
            else if (v.TrackType == TrackTypes.Official) currentScore += 100;
            
            // Penalties
            if (v.TrackType == TrackTypes.Lyrics) currentScore -= 50;
            if (v.TrackType == TrackTypes.Karaoke) currentScore -= 200;

            if (titleLower.Contains(query.ToLower())) currentScore += 50;

            return new { Video = v, Score = currentScore };
        })
        .OrderByDescending(x => x.Score)
        .Select(x => x.Video)
        .ToList();

        // 4. FALLBACK: Only if we have almost zero results AND we haven't already retried
        if (scoredList.Count < 3 && !searchCompilations && depth < 1)
        {
            string fallbackQ = $"{query} official music";
            var fallback = await SearchVideosInternalAsync(fallbackQ, limit, searchCompilations, depth + 1);
            scoredList = scoredList.Concat(fallback).GroupBy(x => x.YoutubeVideoId).Select(g => g.First()).ToList();
        }

        var final = scoredList.Take(limit).ToList();
        // FIX PERF: Tăng TTL từ 20 phút → 2 giờ (YouTube search results ổn định)
        _cache.Set(cacheKey, final, TimeSpan.FromHours(2));
        return final;
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
        string cacheKey = $"trending_music_v11_{limit}";

        try {
            var now = DateTime.UtcNow;
            var monthYear = now.ToString("MMMM yyyy");
            var queries = new List<string> {
                $"Nhạc Việt mới nhất {monthYear}",
                $"YouTube Music Trends Global {now.Year}",
                "V-Pop Top Trending official music",
                $"Nhạc Trẻ {monthYear} hay nhất mới ra",
                "Nhạc Việt hay nhất hiện nay official",
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

    private async Task<YoutubeVideoDetails> MapToFullDetailsAsync(IVideo v, bool enrich = true)
    {
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

        if (enrich)
        {
            return await EnrichVideoDetailsAsync(details);
        }

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
    /// <summary>
    /// Detects ranking/chart/billboard videos that should NEVER appear on the UI.
    /// These are videos like "BXH Nhạc Trẻ", "Top 100 bài hát", "Billboard Hot 100", etc.
    /// </summary>
    public bool IsRankingVideo(YoutubeVideoDetails details)
    {
        var title = details.Title.ToLower();
        var author = details.AuthorName?.ToLower() ?? "";
        
        // Check author/channel name for ranking channel indicators
        var rankingChannelKeywords = new[] {
            "bảng xếp hạng", "xếp hạng", "top music", "ranking", "billboard"
        };
        if (rankingChannelKeywords.Any(k => author.Contains(k))) return true;
        
        // Regex: "top" followed by a number (like "top 5", "top 30")
        // Exception FIRST: short titles like "Top of the World" (a real song, < 25 chars)
        if (title.Contains("top") && title.Length < 25)
        {
            // Only allow if it doesn't contain a number after "top"
            if (!System.Text.RegularExpressions.Regex.IsMatch(title, @"top\s+\d+"))
                return false; // Short title without number → real song, allow
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(title, @"top\s+\d+"))
            return true;
        
        // Comprehensive ranking keywords
        var rankingKeywords = new[] { 
            "top 10", "top 20", "top 50", "top 100",
            "bxh", "bảng xếp hạng", "billboard", "chart", "ranking", "countdown",
            "xếp hạng", "top bài hát", "top music", "top hits",
            "top trending", "top ca khúc", "top nhạc",
            "top vpop", "top v-pop", "top nhạc trẻ", "top ca sĩ",
            "top nghệ sĩ", "top album", "top songs", "top tracks",
            "nhiều lượt xem nhất", "lượt xem nhiều nhất",
            "most viewed", "most popular",
            "hot nhất tháng", "hay nhất tuần"
        };
            
        return rankingKeywords.Any(k => title.Contains(k));
    }

    /// <summary>
    /// Detects content in unwanted languages/regions (Indian, Arabic, etc.)
    /// Only Vietnamese, English, Korean, Japanese, Chinese content is allowed.
    /// </summary>
    public bool IsUnwantedContent(string title, string author)
    {
        var t = title.ToLower();
        var a = author.ToLower();
        
        // Language/region keywords to exclude
        var unwantedKeywords = new[] {
            // Indian languages
            "hindi", "bollywood", "punjabi", "bhojpuri", "tamil song", "telugu song",
            "kannada", "malayalam", "marathi", "gujarati", "bengali song",
            "desi", "tollywood", "kollywood", "mollywood",
            "new hindi", "hindi song", "hindi music", "hindi official",
            "new punjabi", "punjabi song", "punjabi music",
            // Arabic/Middle Eastern
            "arabic", "nasheed", "urdu", "qawwali", "ghazal",
            // Other non-target
            "reggaeton", "bachata"
        };
        
        if (unwantedKeywords.Any(k => t.Contains(k))) return true;
        
        // Known Indian/Arabic music channels to block
        var unwantedChannels = new[] {
            "speed records", "t-series", "zee music", "tips official", "shemaroo",
            "saregama", "yrf", "sony music india", "eros now", "venus",
            "geet mp3", "white hill music", "desi music factory",
            "aditya music", "mango music", "lahari music",
            "anand audio", "rotana", "melody", "music world"
        };
        
        if (unwantedChannels.Any(k => a.Contains(k))) return true;
        
        // Detect non-Latin scripts (Devanagari, Arabic, Gurmukhi/Punjabi, Bengali, Tamil, Telugu)
        // These indicate Indian/Arabic content that is outside target audience
        if (System.Text.RegularExpressions.Regex.IsMatch(title, @"[\u0900-\u097F]")) return true;  // Devanagari (Hindi)
        if (System.Text.RegularExpressions.Regex.IsMatch(title, @"[\u0600-\u06FF]")) return true;  // Arabic
        if (System.Text.RegularExpressions.Regex.IsMatch(title, @"[\u0A00-\u0A7F]")) return true;  // Gurmukhi (Punjabi)
        if (System.Text.RegularExpressions.Regex.IsMatch(title, @"[\u0980-\u09FF]")) return true;  // Bengali
        if (System.Text.RegularExpressions.Regex.IsMatch(title, @"[\u0B80-\u0BFF]")) return true;  // Tamil
        if (System.Text.RegularExpressions.Regex.IsMatch(title, @"[\u0C00-\u0C7F]")) return true;  // Telugu
        if (System.Text.RegularExpressions.Regex.IsMatch(title, @"[\u0D00-\u0D7F]")) return true;  // Malayalam
        if (System.Text.RegularExpressions.Regex.IsMatch(title, @"[\u0E00-\u0E7F]")) return true;  // Thai
        
        return false;
    }

    /// <summary>
    /// Checks if a song is a pure individual track (not ranking, not too long, not karaoke).
    /// Used by ALL sections EXCEPT "Nhạc tổng hợp".
    /// Strict 7-minute limit for non-official content, 10-minute limit for official MVs.
    /// </summary>
    public bool IsMusic(YoutubeVideoDetails details)
    {
        if (IsRankingVideo(details)) return false;
        if (IsKaraoke(details)) return false;
        if (IsUnwantedContent(details.Title, details.AuthorName)) return false;
        if (IsPlaylistOrWeeklyCompilation(details.Title, details.AuthorName)) return false;
        if (HasSingleTrackViolations(details.Title, details.AuthorName)) return false;
        if (IsNonPureTrackType(details.TrackType)) return false;
        
        var title = details.Title.ToLower();
        
        // Duration filtering: strict 7-minute limit, relaxed to 10 mins for official MVs
        if (details.Duration.HasValue)
        {
            bool isOfficialContent = title.Contains("official") || title.Contains("mv");
            double maxMinutes = isOfficialContent ? 10.0 : 7.0;
            if (details.Duration.Value.TotalMinutes > maxMinutes) return false;
        }
        
        // Exclude compilation-type keywords (nonstop, tổng hợp, tuyển tập, mashup, tiktok, etc.)
        var compilationKeywords = new[] {
            "nonstop", "tổng hợp", "tuyển tập", "collection", "best of",
            "full album", "nhạc tuyển tập", "tổng hợp nhạc",
            "liên khúc", "dễ ngủ", "nghe cả ngày", "nghe hoài không chán",
            "mashup", "tiktok", "megamix", "medley"
        };
        if (compilationKeywords.Any(k => title.Contains(k))) return false;
        
        // Exclude viral/trends compilation videos
        if ((title.Contains("viral") && (title.Contains("trend") || title.Contains("music"))) ||
            (title.Contains("trend") && title.Contains("music")))
            return false;
        
        // Generic "nhạc + mood descriptor" titles are always compilations, not individual songs
        if (System.Text.RegularExpressions.Regex.IsMatch(title, @"^nhạc\s+(chill|buồn|hay|hot|trẻ|trữ tình|sôi động|tập trung|thư giãn|lofi|lo-fi|edm|remix|acoustic|piano|piano nhẹ)"))
            return false;
        
        return IsLikelyMusicCore(title, details.AuthorName, details.Duration);
    }

    /// <summary>
    /// Phát hiện playlist tổng hợp theo tuần/tháng và các kênh chuyên làm compilation.
    /// Ví dụ: "New Songs Of The Week", "New Music Friday", "Best Songs This Month", v.v.
    /// </summary>
    private bool IsPlaylistOrWeeklyCompilation(string title, string author)
    {
        var t = title.ToLower();
        var a = (author ?? "").ToLower();

        // --- PATTERN 1: Weekly/Monthly playlist titles ---
        // "New Songs Of The Week", "Songs Of The Week", "Music Of The Week"
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\bof\s+the\s+(week|month|year)\b")) return true;
        // "This Week", "This Month" in music context
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\bthis\s+(week|month)\b") &&
            (t.Contains("song") || t.Contains("music") || t.Contains("hit") || t.Contains("new"))) return true;
        // "New Music Friday", "New Music Monday", etc.
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\bnew\s+music\s+(friday|monday|tuesday|wednesday|thursday|saturday|sunday)\b")) return true;
        // "Weekly", "Monthly" music roundups
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(weekly|monthly)\s+(hits|songs|music|playlist|mix|chart)\b")) return true;
        // "New Releases", "New Songs" as a collection title (not a single song)
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^new\s+(songs|releases|music|hits)\b") &&
            (t.Contains("week") || t.Contains("month") || t.Contains("april") || t.Contains("march") ||
             t.Contains("january") || t.Contains("february") || t.Contains("may") || t.Contains("june") ||
             t.Contains("july") || t.Contains("august") || t.Contains("september") || t.Contains("october") ||
             t.Contains("november") || t.Contains("december") || t.Contains("2025") || t.Contains("2026")))
            return true;
        // "Best New Songs", "Best Songs April 2026"
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\bbest\s+(new\s+)?(songs|music|hits)\b") &&
            (t.Contains("week") || t.Contains("month") || System.Text.RegularExpressions.Regex.IsMatch(t, @"\b20\d\d\b")))
            return true;
        // "Hot This Week", "Trending This Week"
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(hot|trending|popular)\s+this\s+(week|month)\b")) return true;

        // --- PATTERN 2: Playlist/compilation channel indicators ---
        // Channels that ONLY make weekly/monthly compilation playlists
        var compilationChannels = new[] {
            "inmusic", "in music", "new music",
            "music weekly", "weekly music", "songs weekly",
            "music friday", "new releases", "fresh music",
            "music chart", "chart music", "hit music",
            "music playlist", "playlist music",
            "top music official", "music official top"
        };
        if (compilationChannels.Any(k => a.Contains(k))) return true;

        // --- PATTERN 3: Title structure "Songs Of [Month] [Year]" ---
        // "Songs Of April 2026", "Hits Of March 2025"
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(songs|hits|music)\s+of\s+(january|february|march|april|may|june|july|august|september|october|november|december)\b"))
            return true;

        // --- PATTERN 4: Numbered/dated collection titles ---
        // "New Songs (April 3, 2026)", "New Songs (Week 14)"
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^new\s+songs?\s*\(") ||
            System.Text.RegularExpressions.Regex.IsMatch(t, @"^new\s+music\s*\("))
            return true;

        // --- PATTERN 5: "Mix" + date/period patterns ---
        // "April Mix 2026", "Spring Mix 2026"
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(january|february|march|april|may|june|july|august|september|october|november|december|spring|summer|fall|autumn|winter)\s+mix\b"))
            return true;

        return false;
    }

    /// <summary>
    /// Legacy method kept for GetCompilationsAsync.
    /// Checks if a video is a compilation (long music, nonstop, tổng hợp, etc.).
    /// EXCLUDES ranking videos — compilations section should never show BXH/chart videos.
    /// </summary>
    public bool IsCompilation(YoutubeVideoDetails details)
    {
        // NEVER allow ranking videos, even in compilations
        if (IsRankingVideo(details)) return false;
        // NEVER allow unwanted language content
        if (IsUnwantedContent(details.Title, details.AuthorName)) return false;
        
        var title = details.Title.ToLower();
        
        var compilationKeywords = new[] {
            "tổng hợp", "nonstop", "collection", "best of",
            "tuyển tập", "tổng hợp nhạc", "nhạc tuyển tập",
            "full album", "playlist"
        };

        bool hasCompilationKeyword = compilationKeywords.Any(k => title.Contains(k));
        
        // Also consider long videos (> 7 mins) as potential compilations
        bool isLongVideo = details.Duration.HasValue && details.Duration.Value.TotalMinutes > 7.0;
        
        return hasCompilationKeyword || isLongVideo;
    }

    public bool IsKaraoke(YoutubeVideoDetails details)
    {
        var title = details.Title.ToLower();
        // "beat" chỉ bị chặn khi đứng một mình hoặc kết hợp với "chuẩn"/"gốc"
        // Tránh chặn "Beat It", "Heartbeat", "Deadbeat" v.v.
        bool isBeat = System.Text.RegularExpressions.Regex.IsMatch(title, @"\bbeat\b") &&
                      (title.Contains("beat chuẩn") || title.Contains("beat gốc") ||
                       title.Contains("phối beat") || title.Contains("nhạc beat") ||
                       System.Text.RegularExpressions.Regex.IsMatch(title, @"^beat\s") ||
                       System.Text.RegularExpressions.Regex.IsMatch(title, @"\s+beat$"));
        return title.Contains("karaoke") || isBeat || title.Contains("tách lời") || title.Contains("không lời");
    }

    private bool IsLikelyMusic(YoutubeExplode.Search.VideoSearchResult v, bool searchCompilations = false)
    {
        return IsLikelyMusicCore(v.Title.ToLower(), v.Author.ChannelTitle, v.Duration, searchCompilations);
    }

    private bool IsLikelyMusicCore(string title, string author, TimeSpan? duration, bool searchCompilations = false)
    {
        var t = title.ToLower();
        
        // Unwanted language/region content — ALWAYS excluded
        if (IsUnwantedContent(title, author)) return false;

        // Weekly/monthly playlist compilations — ALWAYS excluded from individual track sections
        if (!searchCompilations && IsPlaylistOrWeeklyCompilation(title, author)) return false;
        if (!searchCompilations && HasSingleTrackViolations(title, author)) return false;
        
        // Ranking videos — ALWAYS excluded
        var authorLower = author.ToLower();
        var rankingChannelKeywords = new[] { "bảng xếp hạng", "xếp hạng", "top music", "ranking", "billboard" };
        if (rankingChannelKeywords.Any(k => authorLower.Contains(k))) return false;

        // "top N" pattern — always ranking
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"top\s+\d+")) return false;

        // Short "top" titles without number → real song (e.g. "Top of the World")
        if (t.Contains("top") && t.Length < 25 && !System.Text.RegularExpressions.Regex.IsMatch(t, @"top\s+\d+"))
        {
            // Allow — fall through to other checks
        }
        else
        {
            var rankingKeywords = new[] { 
                "bxh", "bảng xếp hạng", "billboard", "chart", "ranking", "countdown",
                "xếp hạng", "top bài hát", "top music", "top hits",
                "top trending", "top ca khúc", "top nhạc",
                "top vpop", "top v-pop", "top nhạc trẻ", "top ca sĩ",
                "top nghệ sĩ", "top album", "top songs", "top tracks",
                "nhiều lượt xem nhất", "lượt xem nhiều nhất",
                "most viewed", "most popular",
                "hot nhất tháng", "hay nhất tuần"
            };
            if (rankingKeywords.Any(k => t.Contains(k))) return false;
        }

        if (searchCompilations)
        {
            // For explicitly searching compilations, allow long content but still block ranking
            return true;
        }

        // Strict 7-minute limit for regular discovery
        bool isOfficialContent = t.Contains("official") || t.Contains("mv");
        double maxMinutes = isOfficialContent ? 10.0 : 7.0;
        if (duration.HasValue && duration.Value.TotalMinutes > maxMinutes) return false;
        
        // Exclude compilation-type keywords in regular search
        var compilationKeywords = new[] {
            "nonstop", "tổng hợp", "tuyển tập", "collection", "best of",
            "full album", "nhạc tuyển tập", "tổng hợp nhạc",
            "liên khúc", "dễ ngủ", "nghe cả ngày", "nghe hoài không chán",
            "mashup", "tiktok", "megamix", "medley"
        };
        if (compilationKeywords.Any(k => t.Contains(k))) return false;
        
        // Exclude viral/trends compilations
        if ((t.Contains("viral") && (t.Contains("trend") || t.Contains("music"))) ||
            (t.Contains("trend") && t.Contains("music")))
            return false;
        
        // Generic "nhạc + mood descriptor" titles are compilations
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^nhạc\s+(chill|buồn|hay|hot|trẻ|trữ tình|sôi động|tập trung|thư giãn|lofi|lo-fi|edm|remix|acoustic|piano)"))
            return false;
        
        // Exclude karaoke
        if (t.Contains("karaoke") || t.Contains("tách lời")) return false;
        
        return true;
    }

    /// <summary>
    /// Reject non-single variants: remix/mix/live set/cover/sped/slowed/reverb/mashup...
    /// Chỉ chặn các dạng KHÔNG phải bài hát đơn lẻ chính thức.
    /// Live performance và Cover chính thức vẫn được cho qua.
    /// </summary>
    private bool HasSingleTrackViolations(string title, string author)
    {
        var t = title.ToLower();
        var a = (author ?? string.Empty).ToLower();

        // Chặn các biến thể kỹ thuật rõ ràng không phải bài gốc
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(sped\s*up|slowed|reverb|nightcore|phonk)\b"))
            return true;

        // Chặn mashup và megamix (nhiều bài ghép lại)
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(mashup|megamix|medley)\b"))
            return true;

        // Chặn DJ set / full set (không phải bài đơn)
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(dj\s+set|full\s+set|live\s+set)\b"))
            return true;

        // Chặn playlist/full set rõ ràng
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(full\s+set|megamix)\b"))
            return true;

        // Chặn kênh chuyên remix/nightcore
        var nonSingleChannelHints = new[] { "nightcore", "mashup channel", "mix channel", "remix channel" };
        if (nonSingleChannelHints.Any(k => a.Contains(k))) return true;

        // KHÔNG chặn: remix (có thể là official remix), mix (có thể là "mix" trong tên bài),
        // live (live performance chính thức), cover (cover chính thức), acoustic version
        return false;
    }

    private bool IsNonPureTrackType(string? trackType)
    {
        // Chỉ chặn Compilation và Karaoke — không chặn Remix, Live, Cover vì chúng có thể là nội dung chất lượng
        return string.Equals(trackType, TrackTypes.Compilation, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trackType, TrackTypes.Karaoke, StringComparison.OrdinalIgnoreCase);
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
        if (t.Contains("karaoke") || t.Contains("beat") || t.Contains("beat chuẩn") || t.Contains("tách lời")) return TrackTypes.Karaoke;
        if (t.Contains("tổng hợp") || t.Contains("full album") || t.Contains("nonstop") || t.Contains("collection")) return TrackTypes.Compilation;
        if (t.Contains("official music video") || t.Contains("official mv") || (t.Contains("mv") && t.Contains("official"))) return TrackTypes.OfficialMV;
        if (t.Contains("official audio") || t.Contains("official music audio")) return TrackTypes.OfficialAudio;
        if (t.Contains("official video") || t.Contains("official lyric") || t.Contains("official visualizer")) return TrackTypes.Official;
        
        if (t.Contains("remix") || t.Contains("mix")) return TrackTypes.Remix;
        if (t.Contains("live") || t.Contains("concert")) return TrackTypes.Live;
        if (t.Contains("cover")) return TrackTypes.Cover;
        if (t.Contains("lyric")) return TrackTypes.Lyrics;
        if (t.Contains("acoustic")) return TrackTypes.Acoustic;
        
        return TrackTypes.Unknown;
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

            // Expand list to all available tracks to ensure switcher (LYR-05) is populated.
            // Prioritize vi and en for the top of the list if we want, but unique list is key.
            return trackManifest.Tracks
                .Select(t => new CaptionTrackDto
                {
                    LanguageCode = t.Language.Code,
                    LanguageName = t.Language.Name,
                    IsAutoGenerated = t.IsAutoGenerated
                })
                .GroupBy(x => x.LanguageCode) 
                .Select(g => g.First())
                .OrderByDescending(t => t.LanguageCode == "vi")
                .ThenByDescending(t => t.LanguageCode == "en")
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

