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

namespace YoutubeMusicPlayer.Infrastructure.External;

public class YoutubeService : IYoutubeService
{
    private readonly YoutubeClient _youtube = new YoutubeClient();
    private readonly IMemoryCache _cache;
    private readonly ISpotifyService _spotifyService;

    public YoutubeService(IMemoryCache cache, ISpotifyService spotifyService)
    {
        _cache = cache;
        _spotifyService = spotifyService;
    }

    public async Task<string> GetAudioStreamUrlAsync(string videoUrl, string? title = null, string? artist = null)
    {
        string youtubeId = videoUrl;
        if (videoUrl.Contains("v=")) youtubeId = videoUrl.Split("v=").Last().Split("&").First();

        string cacheKey = $"stream_v4_{youtubeId}";
        if (_cache.TryGetValue(cacheKey, out string? cachedUrl)) return cachedUrl!;

        try 
        {
            return await GetUrlInternalAsync(youtubeId, cacheKey);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YoutubeService] Primary stream failed for {youtubeId}: {ex.Message}. Attempting RECOVERY...");
            
            try 
            {
                // RECOVERY LOGIC: Use provided Title/Artist or fetch them if missing
                string fallbackQuery = "";
                if (!string.IsNullOrEmpty(title))
                {
                    fallbackQuery = $"{title} {artist} audio lyrics";
                }
                else
                {
                    try {
                        var video = await _youtube.Videos.GetAsync(youtubeId);
                        fallbackQuery = $"{video.Title} {video.Author.ChannelTitle} audio lyrics";
                    } catch {
                        // If GetAsync also fails, we can't do much without external info
                        throw new Exception("Cannot fetch metadata for recovery.");
                    }
                }

                var searchResults = await _youtube.Search.GetVideosAsync(fallbackQuery).CollectAsync(3);
                
                // Try up to 3 results
                foreach(var fallbackVideo in searchResults)
                {
                    if (fallbackVideo.Id == youtubeId) continue;
                    try {
                        Console.WriteLine($"[YoutubeService] Trying fallback: {fallbackVideo.Id} ({fallbackVideo.Title})");
                        return await GetUrlInternalAsync(fallbackVideo.Id, cacheKey);
                    } catch { continue; }
                }
            }
            catch (Exception recoveryEx)
            {
                Console.WriteLine($"[YoutubeService] Recovery also failed: {recoveryEx.Message}");
            }

            throw new Exception("This video is restricted. Attempted recovery but no alternative audio was found.");
        }
    }

    private async Task<string> GetUrlInternalAsync(string youtubeId, string cacheKey)
    {
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(youtubeId);
        var streamOptions = manifest.GetAudioOnlyStreams().OrderByDescending(s => s.Bitrate).ToList();
        
        if (!streamOptions.Any()) throw new Exception("No audio streams found.");

        var url = streamOptions.First().Url;
        Console.WriteLine($"[YoutubeService] Successfully extracted stream for {youtubeId}: {url.Substring(0, 50)}...");
        _cache.Set(cacheKey, url, TimeSpan.FromMinutes(30));
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

    public async Task<IEnumerable<YoutubeVideoDetails>> SearchVideosAsync(string query, int limit = 30)
    {
        string cacheKey = $"search_v6_{query}_{limit}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        // 1. Optimize query for MUSIC only
        string optimizedQuery = query.EndsWith("music") || query.EndsWith("songs") ? query : $"{query} official music audio";
        
        var results = await _youtube.Search.GetVideosAsync(optimizedQuery).CollectAsync(30); 
        var filtered = results.Where(IsLikelyMusic).ToList();

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

    public async Task<IEnumerable<YoutubeVideoDetails>> GetTrendingMusicAsync(int limit = 15)
    {
        string cacheKey = $"trending_music_v7_{limit}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        try {
            var queries = new List<string> {
                "Thịnh hành âm nhạc Việt Nam mới nhất",
                "Top 100 YouTube Music Vietnam",
                "Global Hits 2024 Official Music"
            };

            var tasks = queries.Select(q => SearchVideosAsync(q, limit / 2));
            var resultsArray = await Task.WhenAll(tasks);
            
            var allResults = resultsArray.SelectMany(x => x).ToList();

            var uniqueResults = allResults
                .GroupBy(v => v.YoutubeVideoId)
                .Select(g => g.First())
                .OrderByDescending(v => v.TrackType == "Official")
                .Take(limit)
                .ToList();

            _cache.Set(cacheKey, uniqueResults, TimeSpan.FromHours(6));
            return uniqueResults;
        } catch { return Enumerable.Empty<YoutubeVideoDetails>(); }
    }

    private async Task<YoutubeVideoDetails> MapToFullDetailsAsync(IVideo v)
    {
        // NO Spotify enrichment during search to avoid 429
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
        return IsLikelyMusicCore(details.Title, details.AuthorName, details.Duration);
    }

    private bool IsLikelyMusic(YoutubeExplode.Search.VideoSearchResult v)
    {
        return IsLikelyMusicCore(v.Title, v.Author.ChannelTitle, v.Duration);
    }

    private bool IsLikelyMusicCore(string title, string author, TimeSpan? duration)
    {
        // Broaden for testing/stabilization
        if (duration.HasValue && duration.Value.TotalMinutes > 30) return false;
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

        // SYNC WITH SPOTIFY (Safe enrichment)
        try {
            await TryEnrichWithSpotifyAsync(details);
        } catch (Exception ex) {
            Console.WriteLine($"[YoutubeService] Metadata enrichment warning: {ex.Message}");
        }

        details.Tags = ExtractAI_Tags(details); 

        return details;
    }

    private async Task TryEnrichWithSpotifyAsync(YoutubeVideoDetails details)
    {
        try {
            var spotifyTrack = await _spotifyService.SearchTrackAsync(details.CleanedTitle, details.CleanedArtist);
            if (spotifyTrack != null)
            {
                details.CleanedArtist = spotifyTrack.ArtistName;
                details.CleanedTitle = spotifyTrack.TrackName;
                if (spotifyTrack.Genres.Any())
                {
                    details.Genre = NormalizeGenre(spotifyTrack.Genres.First());
                }
            }
        } catch { }
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
        if (t.Contains("remix") || t.Contains("mix") || t.Contains("nonstop")) return "Remix/Mix";
        if (t.Contains("live") || t.Contains("concert")) return "Live";
        if (t.Contains("cover")) return "Cover";
        if (t.Contains("lyric")) return "Lyrics";
        if (t.Contains("acoustic")) return "Acoustic";
        return "Official";
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

