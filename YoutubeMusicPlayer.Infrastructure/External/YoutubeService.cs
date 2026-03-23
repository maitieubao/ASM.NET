using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Search;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Infrastructure.External;

public class YoutubeService : IYoutubeService
{
    private readonly YoutubeClient _youtube = new YoutubeClient();
    private readonly IMemoryCache _cache;

    public YoutubeService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<string> GetAudioStreamUrlAsync(string videoUrl)
    {
        string cacheKey = $"stream_{videoUrl}";
        if (_cache.TryGetValue(cacheKey, out string? cachedUrl))
        {
            return cachedUrl!;
        }

        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl);
        var url = manifest.GetAudioOnlyStreams().OrderByDescending(s => s.Bitrate).First().Url;

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
            Hashtags = hashtags,
            Genre = genre
        };

        details = EnrichVideoDetails(details);

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

        var filteredResults = allVideos
            .Where(v => {
                var title = v.Title.ToLower();
                bool isMusic = musicKeywords.Any(k => title.Contains(k)) || v.Duration > TimeSpan.FromMinutes(1);
                bool isExcluded = excludeKeywords.Any(k => title.Contains(k));
                return isMusic && !isExcluded;
            })
            .Take(50) // Limit to 50 for performance
            .Select(v => EnrichVideoDetails(new YoutubeVideoDetails
            {
                Title = v.Title,
                AuthorName = v.Author.ChannelTitle,
                AuthorChannelId = v.Author.ChannelId,
                YoutubeVideoId = v.Id,
                ThumbnailUrl = v.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url,
                Duration = v.Duration,
                Genre = GuessGenre(v.Title, new List<string>())
            })).ToList();

        _cache.Set(cacheKey, filteredResults, TimeSpan.FromHours(12));
        return filteredResults;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> SearchVideosAsync(string query)
    {
        string cacheKey = $"search_v2_{query}"; // Versioned cache key
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cachedResults))
        {
            return cachedResults!;
        }

        // 1. Optimize query for MUSIC only
        string optimizedQuery = query.EndsWith("music") ? query : $"{query} official music audio";
        
        var results = await _youtube.Search.GetVideosAsync(optimizedQuery).CollectAsync(30); // Get more for filtering
        
        var searchResponses = results
            .Where(IsLikelyMusic)
            .Select(v => EnrichVideoDetails(new YoutubeVideoDetails
            {
                Title = v.Title,
                AuthorName = v.Author.ChannelTitle,
                YoutubeVideoId = v.Id,
                ThumbnailUrl = v.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url,
                Duration = v.Duration,
                Genre = GuessGenre(v.Title, new List<string>())
            }))
            .Take(15) // Return clean top 15
            .ToList();

        _cache.Set(cacheKey, searchResponses, TimeSpan.FromMinutes(10));
        return searchResponses;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetRelatedVideosAsync(string videoId)
    {
        var original = await GetVideoDetailsAsync(videoId);
        
        // --- 1. RELIABLE BUCKETS ---
        // Gắn nhiều trọng số vào Artist thay vì Genre theo yêu cầu
        var bucketArtist = await FetchCandidates($"{original.CleanedArtist} official music", 20, original.Genre);
        var bucketSameGenre = await FetchCandidates($"{original.Genre} songs", 10, original.Genre);
        var bucketPlaylist = await FetchCandidates($"{original.CleanedTitle} playlist", 5, original.Genre);
        var bucketTrending = await FetchCandidates("top music hits 2024", 5, original.Genre);

        var allCandidates = bucketArtist.Concat(bucketSameGenre).Concat(bucketPlaylist).Concat(bucketTrending)
            .Where(IsMusic)
            .GroupBy(v => v.YoutubeVideoId)
            .Select(g => g.First())
            .Where(v => v.YoutubeVideoId != videoId)
            // HARD FILTER: Completely drop duplicates and same songs before scoring
            .Where(v => {
                var isTitleMatch = v.CleanedTitle.Contains(original.CleanedTitle, StringComparison.OrdinalIgnoreCase) 
                                || original.CleanedTitle.Contains(v.CleanedTitle, StringComparison.OrdinalIgnoreCase);
                return !isTitleMatch && !IsTooSimilar(original.CleanedTitle, v.CleanedTitle, 0.5);
            })
            .ToList();

        // --- 2. SIMPLE SCORING ---
        var scoredList = allCandidates.Select(v => {
            int score = 0;
            bool sameGenre = v.Genre == original.Genre;
            bool sameArtist = v.CleanedArtist.Equals(original.CleanedArtist, StringComparison.OrdinalIgnoreCase);
            int tagMatch = v.Tags.Intersect(original.Tags).Count();

            // Simple rules as requested
            if (sameArtist) score += 60; // Priority: Same artist!
            if (sameGenre) score += 20; 
            if (tagMatch > 0) score += tagMatch * 5;
            
            // Add a little flavor from popularity
            if (v.PopularityScore > 0) score += 10;

            return new { Video = v, Score = score, SameArtist = sameArtist, SameGenre = sameGenre };
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        // --- 3. HYBRID MIXING (EXACTLY AS DEFINED: Focus on Same Artist) ---
        // 70% Same Artist (Take 14)
        var sameArtistGroup = scoredList.Where(x => x.SameArtist).Select(x => x.Video).ToList(); 
        
        // 20% Same Genre + Diff Artist (Take 4)
        var sameGenreDiffArtist = scoredList.Where(x => x.SameGenre && !x.SameArtist).Select(x => x.Video).ToList();
        
        // 10% Trending / Random (Take 2)
        var trendingOrRandom = scoredList.Where(x => !x.SameGenre && !x.SameArtist).OrderBy(_ => Guid.NewGuid()).Select(x => x.Video).ToList();

        var finalResults = new List<YoutubeVideoDetails>();
        finalResults.AddRange(sameArtistGroup.Take(14));
        finalResults.AddRange(sameGenreDiffArtist.Take(4));
        finalResults.AddRange(trendingOrRandom.Take(2));

        return finalResults;
    }

    private async Task<List<YoutubeVideoDetails>> FetchCandidates(string query, int count, string contextGenre)
    {
        if (string.IsNullOrEmpty(query)) return new List<YoutubeVideoDetails>();
        
        try {
            var results = await _youtube.Search.GetVideosAsync(query).CollectAsync(count);
            return results.Select(v => EnrichVideoDetails(new YoutubeVideoDetails
            {
                Title = v.Title,
                AuthorName = v.Author.ChannelTitle,
                YoutubeVideoId = v.Id,
                ThumbnailUrl = v.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url,
                Duration = v.Duration,
                // CRITICAL FIX: Re-guess genre for each candidate instead of hardcoding
                Genre = GuessGenre(v.Title, new List<string>())
            })).ToList();
        } catch { return new List<YoutubeVideoDetails>(); }
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
        title = title.ToLower();
        author = author.ToLower();

        // 1. Keywords
        var musicKeywords = new[] { "official", "music", "audio", "lyric", "mv", "remix", "track", "v-pop", "k-pop", "album", "concert" };
        var mixKeywords = new[] { "mix", "nonstop", "tuyển tập", "playlist", "best of", "lofi", "chill", "mashup", "liên khúc" };
        
        bool hasMusicKeyword = musicKeywords.Any(k => title.Contains(k)) || musicKeywords.Any(k => author.Contains(k));
        bool hasMixKeyword = mixKeywords.Any(k => title.Contains(k));

        // 2. Strict Exclusions (Non-music content)
        var excludeKeywords = new[] { "vlog", "podcast", "interview", "challenge", "story", "review", "reaction", "gaming", "tutorial", "news", "movie", "film" };
        bool isExcluded = excludeKeywords.Any(k => title.Contains(k)) || excludeKeywords.Any(k => author.Contains(k));

        // 3. Duration Logic
        if (!duration.HasValue) return false;
        
        // Always ignore extremely short clips
        if (duration < TimeSpan.FromSeconds(90)) return false;
        
        // Special case for long videos (Albums, Mixes, Nonstop)
        if (duration > TimeSpan.FromMinutes(15))
        {
            // Long videos MUST contain mix/album keywords or be from an official Topic channel
            return (hasMixKeyword || hasMusicKeyword || author.Contains("topic") || author.Contains("vevo")) && !isExcluded;
        }

        // Standard song logic
        return (hasMusicKeyword || hasMixKeyword || author.Contains("topic") || author.Contains("vevo")) && !isExcluded;
    }

    private string GuessGenre(string title, List<string> tags)
    {
        title = title.ToLower();
        var allMarkers = tags.Select(t => t.ToLower()).ToList();
        allMarkers.Add(title);

        if (allMarkers.Any(m => m.Contains("kpop") || m.Contains("k-pop") || m.Contains("korean"))) return "K-Pop";
        if (allMarkers.Any(m => m.Contains("us-uk") || m.Contains("usuk") || m.Contains("vevo"))) return "US-UK";
        if (allMarkers.Any(m => m.Contains("classic") || m.Contains("classical") || m.Contains("orchestra") || m.Contains("piano"))) return "Nhạc Classic";
        if (allMarkers.Any(m => m.Contains("pop") || m.Contains("p-o-p"))) return "Nhạc Pop";
        if (allMarkers.Any(m => m.Contains("nhạc trẻ") || m.Contains("vpop") || m.Contains("v-pop") || m.Contains("việt"))) return "Nhạc trẻ";
        if (allMarkers.Any(m => m.Contains("remix") || m.Contains("vinahouse") || m.Contains("nonstop"))) return "Remix";

        return "General";
    }

    private List<string> ExtractHashtags(string description)
    {
        if (string.IsNullOrEmpty(description)) return new List<string>();
        var matches = System.Text.RegularExpressions.Regex.Matches(description, @"#\w+");
        return matches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).ToList();
    }

    // --- DATA ENRICHMENT LAYER ---
    private YoutubeVideoDetails EnrichVideoDetails(YoutubeVideoDetails details)
    {
        var parsed = ParseTitle(details.Title, details.AuthorName);
        
        // Clean Title & Normalize Artist
        details.CleanedTitle = CleanTitleString(parsed.Song);
        details.CleanedArtist = NormalizeArtist(parsed.Artist);
        
        details.TrackType = DetectTrackType(details.Title);
        details.Tags = ExtractAI_Tags(details); // Fake AI Tagging
        
        // Fake popularity scoring based on official signals
        int pop = 0;
        var tLower = details.Title.ToLower();
        var aLower = details.AuthorName.ToLower();
        
        if (tLower.Contains("official")) pop += 20;
        if (tLower.Contains("mv")) pop += 15;
        if (aLower.Contains("vevo") || aLower.Contains("topic")) pop += 30;
        if (details.TrackType == "Official") pop += 10;
        
        details.PopularityScore = pop;
        
        return details;
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

