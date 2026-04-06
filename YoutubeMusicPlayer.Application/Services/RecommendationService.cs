using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class RecommendationService : IRecommendationService
{
    private readonly IYoutubeService _youtubeService;
    private readonly IDeezerService _deezerService;
    private readonly IInteractionService _interactionService;
    private readonly ISongService _songService;
    private readonly IMemoryCache _cache;
    private static readonly Random _rand = new();

    public RecommendationService(
        IYoutubeService youtubeService, 
        IDeezerService deezerService,
        IInteractionService interactionService,
        ISongService songService,
        IMemoryCache cache)
    {
        _youtubeService = youtubeService;
        _deezerService = deezerService;
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
        HashSet<string>? preferredGenres = null;
        HashSet<string>? preferredArtists = null;
        HashSet<string>? historyVideoIds = null;
        Dictionary<string, long>? localPlayCounts = null;

        if (userId.HasValue)
        {
            preferredGenres = (await _interactionService.GetTopPreferredGenresAsync(userId.Value, 5)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            // Optimized: Fetching preferred artists to restore the 80-point weight factor
            var prefArtists = await _interactionService.GetTopPreferredArtistsAsync(userId.Value, 10);
            preferredArtists = prefArtists.ToHashSet(StringComparer.OrdinalIgnoreCase);

            historyVideoIds = (await _interactionService.GetHistoryVideoIdsAsync(userId.Value, SearchSettings.MaxHistoryForDiscovery)).ToHashSet();
        }
        
        localPlayCounts = await _songService.GetUniversalPlayCountsAsync();

        // --- 1. CANDIDATE GATHERING ---
        var fetchTasks = new List<Task<IEnumerable<YoutubeVideoDetails>>>();
        
        var artistQuery = $"{original.CleanedArtist} official music 2026";
        fetchTasks.Add(SafeFetchAsync(() => _youtubeService.SearchVideosAsync(artistQuery)));
        fetchTasks.Add(FetchDeezerDiscoveryAsync(original));
        fetchTasks.Add(SafeFetchAsync(() => _youtubeService.SearchVideosAsync($"{original.Genre} trending music")));

        if (preferredGenres != null && preferredGenres.Any())
        {
            var pQuery = string.Join(" ", preferredGenres.Take(2)) + " top music";
            fetchTasks.Add(SafeFetchAsync(() => _youtubeService.SearchVideosAsync(pQuery)));
        }

        var resultsAll = await Task.WhenAll(fetchTasks);
        var candidatesAll = resultsAll.SelectMany(x => x)
            .GroupBy(v => v.YoutubeVideoId).Select(g => g.First())
            .Where(v => v.YoutubeVideoId != currentVideoId && _youtubeService.IsMusic(v))
            .ToList();

        // --- 2. THE HYBRID RANKING ENGINE ---
        var originalTags = original.Tags.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scoredResults = candidatesAll.Select(v => {
            double score = 0;
            
            if (v.CleanedArtist.Equals(original.CleanedArtist, StringComparison.OrdinalIgnoreCase)) score += 150;
            else if (v.Genre == original.Genre) score += 50;
            
            // LAYER 2: PERSONALIZATION WEIGHT (O(1) lookups)
            if (preferredArtists != null && preferredArtists.Contains(v.CleanedArtist)) { score += 80; v.IsPersonalized = true; }
            if (preferredGenres != null && preferredGenres.Contains(v.Genre)) { score += 40; v.IsPersonalized = true; }
            
            if (historyVideoIds != null && historyVideoIds.Contains(v.YoutubeVideoId)) 
            {
                score -= 100; // Focus on discovery
                v.IsPersonalized = true;
            }

            double globalPopScore = Math.Log10(Math.Max(v.ViewCount, 1)) * 6;
            long localPC = (localPlayCounts != null && localPlayCounts.TryGetValue(v.YoutubeVideoId, out long pc)) ? pc : 0;
            score += globalPopScore + Math.Min(localPC * 5, 80);

            if (v.TrackType == "Official") score += 30;
            score += v.Tags.Count(t => originalTags.Contains(t)) * 10;

            if (!v.CleanedArtist.Equals(original.CleanedArtist, StringComparison.OrdinalIgnoreCase))
                score += _rand.Next(0, 20); 

            return new { Video = v, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        // --- 3. DYNAMIC MIXING ---
        var final = new List<YoutubeVideoDetails>();
        final.AddRange(scoredResults.Take(25).Select(x => x.Video)); // Take larger batch for diversity filter
        
        // Enforce Artist Diversity: Max 3 songs per artist
        var filtered = ApplyDiversityFilter(final, 3).Take(20).ToList();

        _cache.Set(cacheKey, filtered, TimeSpan.FromMinutes(15));
        return filtered;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetDailyMixAsync(int userId, Dictionary<string, double>? prefetchedWeights = null, bool forceRefresh = false)
    {
        return await GetDailyMixVariantAsync(userId, 0, prefetchedWeights, forceRefresh);
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetDailyMixVariantAsync(int userId, int variantIndex, Dictionary<string, double>? prefetchedWeights = null, bool forceRefresh = false)
    {
        string cacheKey = $"daily_mix_{userId}_v{variantIndex}";
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        var genreWeights = prefetchedWeights ?? await _interactionService.GetTopPreferredGenresWithWeightsAsync(userId, 10);
        if (genreWeights == null || !genreWeights.Any())
        {
            return await _youtubeService.GetTrendingMusicAsync(SearchSettings.DefaultDiscoveryLimit);
        }

        var results = new List<YoutubeVideoDetails>();
        var sortedGenres = genreWeights.OrderByDescending(x => x.Value).ToList();
        var selectedGenres = variantIndex == 0 
                             ? sortedGenres.Take(3) 
                             : (variantIndex == 1 ? sortedGenres.Skip(3).Take(3) : sortedGenres.OrderBy(_ => _rand.Next()).Take(3));

        if (!selectedGenres.Any()) selectedGenres = sortedGenres.Take(3);

        var searchTasks = selectedGenres.Select(async g =>
        {
            // Optimized: Reduced fetch buffer to save bandwidth
            var query = $"{g.Key} {GetRandomQueryModifier()}";
            return await SafeFetchAsync(() => _youtubeService.SearchVideosAsync(query, SearchSettings.DefaultFetchBuffer));
        });

        var searchResults = await Task.WhenAll(searchTasks);
        foreach (var sr in searchResults) results.AddRange(sr);

        var finalResult = results.GroupBy(x => x.YoutubeVideoId)
            .Select(g => g.First())
            .Where(v => _youtubeService.IsMusic(v)) 
            .OrderBy(_ => _rand.Next())
            .Take(10) 
            .ToList();
            
        if (finalResult.Count < 10)
        {
            var extra = await _youtubeService.SearchVideosAsync(SearchSettings.TrendingVPopQuery, 10);
            finalResult.AddRange(extra.Where(v => !finalResult.Any(f => f.YoutubeVideoId == v.YoutubeVideoId)).Take(10 - finalResult.Count));
        }

        finalResult = ApplyDiversityFilter(finalResult, 3);
        _cache.Set(cacheKey, finalResult, TimeSpan.FromHours(2));
        return finalResult;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetBecauseYouListenedToAsync(int userId, string? prefetchedArtistName = null, bool forceRefresh = false)
    {
        string? artistName = prefetchedArtistName;
        if (string.IsNullOrEmpty(artistName))
        {
            var history = await _interactionService.GetRecentListeningHistoryAsync(userId, 1);
            if (history != null && history.Any())
            {
                var songs = await _songService.GetSongsByIdsAsync(new List<int> { history.First() });
                artistName = songs.FirstOrDefault()?.AuthorName;
            }
        }

        if (string.IsNullOrEmpty(artistName)) return await _youtubeService.GetTrendingMusicAsync(12);

        string cacheKey = $"contextual_rec_{userId}_{artistName}";
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        var searchResults = await _youtubeService.SearchVideosAsync($"{artistName} radio mix discovery", 20);
        var final = searchResults.Where(v => _youtubeService.IsMusic(v)).Take(10).ToList(); 

        final = ApplyDiversityFilter(final, 2);
        foreach (var v in final) { v.IsPersonalized = true; v.SectionTitle = $"Vì bạn đã nghe {artistName}"; }

        _cache.Set(cacheKey, final, TimeSpan.FromMinutes(30));
        return final;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetMoodMusicAsync(string moodTag, int limit = 12, bool forceRefresh = false)
    {
        string cacheKey = $"mood_v2_{moodTag}_{limit}";
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        // Optimized: Uses global config dictionary
        string query = MoodQueries.GetQuery(moodTag);
        var results = await _youtubeService.SearchVideosAsync(query, Math.Max(limit, 20));
        var final = results.Where(v => _youtubeService.IsMusic(v)).Take(limit).ToList();

        _cache.Set(cacheKey, final, TimeSpan.FromHours(2)); 
        return final;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetCompilationsAsync(int limit = 12, bool forceRefresh = false)
    {
        string cacheKey = $"compilations_v2_{limit}";
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached)) return cached!;

        var queries = new[] { "nhạc tổng hợp hay nhất 2026", "vietnamese music collection full" };
        var tasks = queries.Select(q => SafeFetchAsync(() => _youtubeService.SearchVideosAsync(q, limit, true)));
        var results = await Task.WhenAll(tasks);
        
        var final = results.SelectMany(x => x)
            .GroupBy(v => v.YoutubeVideoId).Select(g => g.First())
            .Where(v => _youtubeService.IsCompilation(v))
            .Take(limit).ToList();

        _cache.Set(cacheKey, final, TimeSpan.FromHours(4));
        return final;
    }

    private async Task<IEnumerable<YoutubeVideoDetails>> FetchDeezerDiscoveryAsync(YoutubeVideoDetails original)
    {
        try 
        {
            var deezerTrack = await _deezerService.SearchTrackAsync(original.CleanedTitle, original.CleanedArtist);
            if (deezerTrack != null && !string.IsNullOrEmpty(deezerTrack.DeezerArtistId))
            {
                var related = (await _deezerService.GetRelatedArtistsAsync(deezerTrack.DeezerArtistId)).Take(3);
                var artistNames = string.Join(" ", related.Select(a => a.Name));
                return await _youtubeService.SearchVideosAsync($"{artistNames} music discovery", 10);
            }
        } catch { /* Silent fail for discovery bucket */ }
        return Enumerable.Empty<YoutubeVideoDetails>();
    }

    private async Task<IEnumerable<YoutubeVideoDetails>> SafeFetchAsync(Func<Task<IEnumerable<YoutubeVideoDetails>>> action)
    {
        try { return await action(); } catch { return Enumerable.Empty<YoutubeVideoDetails>(); }
    }

    private string GetRandomQueryModifier()
    {
        var modifiers = new[] { "official music 2026", "recent hits", "discovery new", "essential music", "trending now" };
        return modifiers[_rand.Next(modifiers.Length)];
    }

    private List<YoutubeVideoDetails> ApplyDiversityFilter(IEnumerable<YoutubeVideoDetails> songs, int maxPerArtist)
    {
        var result = new List<YoutubeVideoDetails>();
        var artistCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in songs)
        {
            string artist = s.AuthorName;
            if (!artistCounts.ContainsKey(artist)) artistCounts[artist] = 0;
            if (artistCounts[artist] < maxPerArtist)
            {
                result.Add(s);
                artistCounts[artist]++;
            }
        }
        return result;
    }
}
