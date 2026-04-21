using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VibeMusic.Application.Common;
using VibeMusic.Application.Interfaces;
using YoutubeExplode.Search;

namespace VibeMusic.Infrastructure.External;

public class YoutubeSearchService : IYoutubeSearchService
{
    private readonly IYoutubeApiClient _apiClient;
    private readonly IMusicContentFilter _contentFilter;
    private readonly ITrackMetadataProcessor _metadataProcessor;
    private readonly IMemoryCache _cache;
    private readonly ILogger<YoutubeSearchService> _logger;

    public YoutubeSearchService(
        IYoutubeApiClient apiClient,
        IMusicContentFilter contentFilter,
        ITrackMetadataProcessor metadataProcessor,
        IMemoryCache cache,
        ILogger<YoutubeSearchService> logger)
    {
        _apiClient = apiClient;
        _contentFilter = contentFilter;
        _metadataProcessor = metadataProcessor;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> SearchVideosAsync(
        string query, int limit = 30, bool searchCompilations = false)
    {
        var cacheKey = $"search_v11_{query}_{limit}_{searchCompilations}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached) && cached != null)
            return cached;

        var results = await SearchVideosInternalAsync(query, limit, searchCompilations, depth: 0);
        _cache.Set(cacheKey, results, TimeSpan.FromHours(2));
        return results;
    }

    public async Task<IEnumerable<YoutubeVideoDetails>> GetTrendingMusicAsync(
        int limit = 15, bool forceRefresh = false)
    {
        string cacheKey = $"trending_music_v11_{limit}";

        if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<YoutubeVideoDetails>? cached) && cached != null)
            return cached;

        try
        {
            var now = DateTime.UtcNow;
            var monthYear = now.ToString("MMMM yyyy");
            var queries = new List<string>
            {
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
                .Where(v => _contentFilter.IsMusic(v))
                .OrderBy(_ => Random.Shared.Next())
                .Take(limit)
                .ToList();

            _cache.Set(cacheKey, uniqueResults, TimeSpan.FromHours(4));
            return uniqueResults;
        }
        catch
        {
            return Enumerable.Empty<YoutubeVideoDetails>();
        }
    }

    private async Task<IEnumerable<YoutubeVideoDetails>> SearchVideosInternalAsync(
        string query, int limit, bool searchCompilations, int depth)
    {
        _logger.LogInformation("[YoutubeSearchService] Searching YouTube with query: {Query}", query);

        // 1. Fetch results from YouTube using ORIGINAL query
        IReadOnlyList<VideoSearchResult> searchResults;
        try
        {
            searchResults = await _apiClient.SearchVideosAsync(query, Math.Max(limit * 3, 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YoutubeSearchService] YouTube Search failed for query: {Query}", query);
            return Enumerable.Empty<YoutubeVideoDetails>();
        }

        // 2. Filter & Map to details
        var rawList = searchResults.ToList();
        var detailsTasks = new List<Task<YoutubeVideoDetails>>();

        for (int i = 0; i < rawList.Count; i++)
        {
            var v = rawList[i];
            if (_contentFilter.IsLikelyMusicCore(v.Title.ToLower(), v.Author.ChannelTitle, v.Duration, searchCompilations))
            {
                detailsTasks.Add(MapToDetailsAsync(v));
            }
        }

        var detailsList = (await Task.WhenAll(detailsTasks)).ToList();

        // 3. SCORING ENGINE
        var scoredList = detailsList.Select((v, index) =>
        {
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
            scoredList = scoredList.Concat(fallback)
                .GroupBy(x => x.YoutubeVideoId)
                .Select(g => g.First())
                .ToList();
        }

        return scoredList.Take(limit).ToList();
    }

    private async Task<YoutubeVideoDetails> MapToDetailsAsync(VideoSearchResult v)
    {
        // Fetch channel avatar (artist photo) with 24h caching
        string authorAvatarUrl = await GetChannelAvatarAsync(v.Author.ChannelId);

        return new YoutubeVideoDetails
        {
            Title = v.Title,
            AuthorName = v.Author.ChannelTitle,
            AuthorChannelId = v.Author.ChannelId,
            AuthorAvatarUrl = authorAvatarUrl,
            YoutubeVideoId = v.Id,
            ThumbnailUrl = v.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url,
            Duration = v.Duration,
            ViewCount = 0, // Search results don't include ViewCount, will be updated later if needed
            TrackType = _metadataProcessor.DetectTrackType(v.Title),
            Genre = _metadataProcessor.GuessGenre(v.Title, new List<string>())
        };
    }

    private async Task<string> GetChannelAvatarAsync(string channelId)
    {
        if (string.IsNullOrEmpty(channelId)) return string.Empty;

        string cacheKey = $"yt_channel_avatar_v1_{channelId}";
        if (_cache.TryGetValue(cacheKey, out string? avatarUrl)) return avatarUrl ?? string.Empty;

        try
        {
            var channel = await _apiClient.GetChannelAsync(channelId);
            var url = channel.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url ?? string.Empty;

            _cache.Set(cacheKey, url, TimeSpan.FromDays(1));
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[YoutubeSearchService] Failed to fetch channel avatar for {ChannelId}: {Msg}", channelId, ex.Message);
            return string.Empty;
        }
    }
}
