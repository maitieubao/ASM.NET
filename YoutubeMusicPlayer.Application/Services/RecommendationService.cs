using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class RecommendationService : IRecommendationService
{
    private readonly IYoutubeService _youtubeService;
    private readonly ISpotifyService _spotifyService;
    private readonly IInteractionService _interactionService;
    private readonly ISongService _songService;
    private readonly IMemoryCache _cache;

    public RecommendationService(
        IYoutubeService youtubeService, 
        ISpotifyService spotifyService,
        IInteractionService interactionService,
        ISongService songService,
        IMemoryCache cache)
    {
        _youtubeService = youtubeService;
        _spotifyService = spotifyService;
        _interactionService = interactionService;
        _songService = songService;
        _cache = cache;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetSmartDiscoveryAsync(string currentVideoId, int? userId = null)
    {
        string cacheKey = $"smart_discovery_{currentVideoId}_{userId ?? 0}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        var original = await _youtubeService.GetVideoDetailsAsync($"https://youtube.com/watch?v={currentVideoId}");
        
        // --- DATA GATHERING (PERSONALIZATION) ---
        List<string>? preferredGenres = null;
        List<string>? preferredArtists = null;
        List<string>? historyVideoIds = null;
        Dictionary<string, long>? localPlayCounts = null;

        if (userId.HasValue)
        {
            preferredGenres = (await _interactionService.GetTopPreferredGenresAsync(userId.Value, 5)).ToList();
            historyVideoIds = await _interactionService.GetHistoryVideoIdsAsync(userId.Value, 50);
        }
        
        // Universal popularity data (from our community)
        localPlayCounts = await _songService.GetUniversalPlayCountsAsync();

        // --- 1. CANDIDATE GATHERING (Multi-Bucket approach) ---
        var fetchTasks = new List<Task<IEnumerable<YoutubeVideoDetails>>>();
        
        // Bucket 1: Contextual Artist (Elite Match)
        fetchTasks.Add(_youtubeService.SearchVideosAsync($"{original.CleanedArtist} official music"));

        // Bucket 2: Spotify related discovery
        var spotifyTrack = await _spotifyService.SearchTrackAsync(original.CleanedTitle, original.CleanedArtist);
        if (spotifyTrack != null && !string.IsNullOrEmpty(spotifyTrack.SpotifyArtistId))
        {
            var related = (await _spotifyService.GetRelatedArtistsAsync(spotifyTrack.SpotifyArtistId)).Take(5);
            foreach (var artist in related)
            {
                fetchTasks.Add(_youtubeService.SearchVideosAsync($"{artist.Name} songs"));
            }
        }

        // Bucket 3: Genre Trending
        fetchTasks.Add(_youtubeService.SearchVideosAsync($"{original.Genre} hot 2024"));

        // Bucket 4: Personalized preferences (User Bubble)
        if (preferredGenres != null && preferredGenres.Any())
        {
            foreach (var g in preferredGenres.Take(2))
                fetchTasks.Add(_youtubeService.SearchVideosAsync($"{g} top music"));
        }

        var candidatesAll = (await Task.WhenAll(fetchTasks)).SelectMany(x => x)
            .GroupBy(v => v.YoutubeVideoId).Select(g => g.First())
            .Where(v => v.YoutubeVideoId != currentVideoId && _youtubeService.IsMusic(v))
            .ToList();

        // --- 2. THE HYBRID RANKING ENGINE ---
        var scoredResults = candidatesAll.Select(v => {
            double score = 0;
            
            // LAYER 1: CONTEXT WEIGHT (Max 150)
            if (v.CleanedArtist.Equals(original.CleanedArtist, StringComparison.OrdinalIgnoreCase)) score += 150;
            else if (v.Genre == original.Genre) score += 50;
            
            // LAYER 2: PERSONALIZATION WEIGHT (Max 150)
            if (preferredArtists != null && preferredArtists.Contains(v.CleanedArtist, StringComparer.OrdinalIgnoreCase)) { score += 80; v.IsPersonalized = true; }
            if (preferredGenres != null && preferredGenres.Contains(v.Genre, StringComparer.OrdinalIgnoreCase)) { score += 40; v.IsPersonalized = true; }
            
            // --- FRESHNESS LOGIC (A+ Feedback) ---
            if (historyVideoIds != null && historyVideoIds.Take(10).Contains(v.YoutubeVideoId)) 
            {
                score -= 100; // Strong penalty for recently listened songs
            }
            else if (historyVideoIds != null && historyVideoIds.Contains(v.YoutubeVideoId)) 
            {
                score += 40; // Slight boost for older history (familiarity)
                v.IsPersonalized = true;
            }

            // LAYER 3: POPULARITY (Global + Local)
            double globalPopScore = Math.Log10(Math.Max(v.ViewCount, 1)) * 6;
            long localPC = (localPlayCounts != null && localPlayCounts.TryGetValue(v.YoutubeVideoId, out long pc)) ? pc : 0;
            double localPopScore = Math.Min(localPC * 5, 80);
            
            score += globalPopScore;
            score += localPopScore;

            // LAYER 4: QUALITY & TAGS
            if (v.TrackType == "Official") score += 30;
            int commonTags = v.Tags.Intersect(original.Tags).Count();
            score += commonTags * 10;

            // --- SERENDIPITY FACTOR ---
            // If the artist is different and score is already decent, add a "Discovery Bonus"
            if (!v.CleanedArtist.Equals(original.CleanedArtist, StringComparison.OrdinalIgnoreCase))
            {
                score += new Random().Next(0, 20); 
            }

            return new { Video = v, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        // --- 3. DYNAMIC MIXING ---
        var final = new List<YoutubeVideoDetails>();
        
        // 40% Elite Confidence
        final.AddRange(scoredResults.Take(8).Select(x => x.Video));
        
        // 40% Smart Discovery (With randomization to avoid bubble)
        var discovery = scoredResults.Skip(8).Take(20).OrderBy(_ => Guid.NewGuid()).Take(8).Select(x => x.Video);
        final.AddRange(discovery);
        
        // 20% Wildcard
        var wildcards = scoredResults.Skip(28).OrderBy(_ => Guid.NewGuid()).Take(4).Select(x => x.Video);
        final.AddRange(wildcards);

        _cache.Set(cacheKey, final, TimeSpan.FromMinutes(15));
        return final;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetDailyMixAsync(int userId, Dictionary<string, double>? prefetchedWeights = null)
    {
        // Variety parameter: we can generate different mixes by shifting the genre pick
        return await GetDailyMixVariantAsync(userId, 0, prefetchedWeights);
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetDailyMixVariantAsync(int userId, int variantIndex, Dictionary<string, double>? prefetchedWeights = null)
    {
        string cacheKey = $"daily_mix_{userId}_v{variantIndex}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        var genreWeights = prefetchedWeights ?? await _interactionService.GetTopPreferredGenresWithWeightsAsync(userId, 10);
        
        // Cold Start Fallback: If no history, return trending
        if (genreWeights == null || !genreWeights.Any())
        {
            return await GetTopChartsAsync("Vietnam", 15);
        }

        var results = new List<YoutubeVideoDetails>();
        
        // Logic: Pick different genre subsets based on variantIndex
        var sortedGenres = genreWeights.OrderByDescending(x => x.Value).ToList();
        var selectedGenres = variantIndex == 0 
                             ? sortedGenres.Take(3) // Mix 1: Top genres
                             : (variantIndex == 1 
                                ? sortedGenres.Skip(3).Take(3) // Mix 2: Secondary interests
                                : sortedGenres.OrderBy(_ => Guid.NewGuid()).Take(3)); // Mix 3: Random interests

        if (!selectedGenres.Any()) selectedGenres = sortedGenres.Take(3);

        var totalListenSeconds = selectedGenres.Sum(s => s.Value);

        var searchTasks = selectedGenres.Select(async g =>
        {
            int countToFetch = 8;
            var query = variantIndex == 1 ? $"{g.Key} new discovery 2024" : $"{g.Key} top hits";
            return await _youtubeService.SearchVideosAsync(query, countToFetch);
        }).ToList();

        var searchResults = await Task.WhenAll(searchTasks);
        foreach (var sr in searchResults) results.AddRange(sr);

        var finalResult = results.GroupBy(x => x.YoutubeVideoId)
            .Select(g => g.First())
            .OrderBy(_ => Guid.NewGuid())
            .Take(15)
            .ToList();

        _cache.Set(cacheKey, finalResult, TimeSpan.FromHours(2));
        return finalResult;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetBecauseYouListenedToAsync(int userId, string? prefetchedArtistName = null)
    {
        string? artistName = prefetchedArtistName;
        if (string.IsNullOrEmpty(artistName))
        {
            var history = await _interactionService.GetRecentListeningHistoryAsync(userId, 1);
            if (history != null && history.Any())
            {
                var songs = await _songService.GetSongsByIdsAsync(history);
                artistName = songs.FirstOrDefault()?.AuthorName;
            }
        }

        if (string.IsNullOrEmpty(artistName))
        {
            // Fallback for Cold Start: Recommended for you (Trending)
            return await _youtubeService.GetTrendingMusicAsync(12);
        }

        string cacheKey = $"contextual_rec_{userId}_{artistName}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        var searchResults = await _youtubeService.SearchVideosAsync($"{artistName} radio mix discovery", 12);
        var final = searchResults.ToList();
        
        foreach (var v in final) 
        { 
            v.IsPersonalized = true; 
            v.SectionTitle = $"Vì bạn đã nghe {artistName}"; 
        }

        _cache.Set(cacheKey, final, TimeSpan.FromMinutes(30));
        return final;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetTopChartsAsync(string region, int limit = 12)
    {
        string cacheKey = $"top_charts_{region}_{limit}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        string query = region.ToLower() == "vietnam" 
                       ? "v-pop top hits 2024 BXH nhạc trẻ" 
                       : "billboard hot 100 official music videos 2024";

        var results = await _youtubeService.SearchVideosAsync(query, limit);
        var final = results.ToList();

        _cache.Set(cacheKey, final, TimeSpan.FromHours(6));
        return final;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetMoodMusicAsync(string moodTag, int limit = 12)
    {
        string cacheKey = $"mood_{moodTag}_{limit}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        string query = moodTag.ToLower() switch
        {
            "chill" or "giai điệu chill" => "lofi hip hop radio study chill",
            "workout" or "tập thể dục" => "gym workout motivation music 2024",
            "focus" or "tập trung" or "tập trung làm việc" => "deep focus music concentration study",
            "party" or "tiệc tùng" or "sôi động" => "party mix 2024 club edm music",
            "sad" or "buồn" or "tâm trạng" => "sad emotional songs acoustic",
            "v-pop hot" or "nhạc trẻ" => "v-pop hay nhất hiện nay 2024",
            _ => $"{moodTag} music best hits"
        };

        var results = await _youtubeService.SearchVideosAsync(query, limit);
        var final = results.ToList();

        _cache.Set(cacheKey, final, TimeSpan.FromHours(2)); // Increased TTL
        return final;
    }
}
