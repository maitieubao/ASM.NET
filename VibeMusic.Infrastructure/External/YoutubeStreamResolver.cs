using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using YoutubeExplode.Videos.Streams;

namespace VibeMusic.Infrastructure.External;

public class YoutubeStreamResolver : IYoutubeStreamResolver
{
    private readonly IYoutubeApiClient _apiClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<YoutubeStreamResolver> _logger;

    public YoutubeStreamResolver(
        IYoutubeApiClient apiClient,
        IMemoryCache cache,
        ILogger<YoutubeStreamResolver> logger)
    {
        _apiClient = apiClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> ResolveAsync(string videoId, string? title = null, string? artist = null)
    {
        // Check cache first
        var cacheKey = $"stream_v5_{videoId}";
        if (_cache.TryGetValue(cacheKey, out string? cachedUrl) && !string.IsNullOrEmpty(cachedUrl))
            return cachedUrl;

        // Try primary resolution
        try
        {
            var url = await ResolveInternalAsync(videoId, cacheKey);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[YoutubeStreamResolver] Primary stream resolution failed for {VideoId}", videoId);
        }

        // Fallback: search for alternatives when title or artist is provided
        if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(artist))
        {
            var query = $"{title} {artist} official audio".Trim();

            try
            {
                var searchResults = await _apiClient.SearchVideosAsync(query, 3);

                foreach (var fallbackVideo in searchResults)
                {
                    if (fallbackVideo.Id == videoId) continue;

                    try
                    {
                        _logger.LogInformation("[YoutubeStreamResolver] Trying fallback: {Title} ({Id})", fallbackVideo.Title, fallbackVideo.Id);
                        // Cache under the original videoId so subsequent calls for the same video hit cache
                        return await ResolveInternalAsync(fallbackVideo.Id, cacheKey);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception recoveryEx)
            {
                _logger.LogWarning(recoveryEx, "[YoutubeStreamResolver] Fallback search failed for {VideoId}", videoId);
            }
        }

        throw new Exception($"Could not resolve stream URL for video {videoId}");
    }

    private async Task<string> ResolveInternalAsync(string videoId, string cacheKey)
    {
        _logger.LogInformation("[YoutubeStreamResolver] Resolving manifest for: {VideoId}", videoId);

        var manifest = await _apiClient.GetStreamManifestAsync(videoId);

        // Priority: M4A Audio-only (best bitrate) -> Other Audio-only -> Muxed
        var audioStreams = manifest.GetAudioOnlyStreams().ToList();

        IStreamInfo? selectedStream = audioStreams
            .OrderByDescending(s => s.Container.Name == "m4a") // Favor m4a for stability in browsers
            .ThenByDescending(s => s.Bitrate)
            .FirstOrDefault();

        if (selectedStream == null)
        {
            _logger.LogWarning("[YoutubeStreamResolver] No audio-only streams found for {VideoId}. Falling back to muxed.", videoId);
            selectedStream = manifest.GetMuxedStreams().OrderByDescending(s => s.VideoQuality).FirstOrDefault();
        }

        if (selectedStream == null)
            throw new Exception("Không tìm thấy luồng âm thanh nào khả dụng.");

        var url = selectedStream.Url;
        _logger.LogInformation("[YoutubeStreamResolver] Selected stream: {Container} ({Bitrate}) for {VideoId}",
            selectedStream.Container, selectedStream.Bitrate, videoId);

        // Cache the URL (5 hours — YouTube URLs are valid for ~6 hours)
        _cache.Set(cacheKey, url, TimeSpan.FromHours(5));
        return url;
    }
}
