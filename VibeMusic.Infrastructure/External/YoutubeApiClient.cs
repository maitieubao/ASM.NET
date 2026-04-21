using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Videos.ClosedCaptions;
using YoutubeExplode.Search;
using YoutubeExplode.Channels;
using YoutubeExplode.Playlists;
using Microsoft.Extensions.Logging;

namespace VibeMusic.Infrastructure.External;

public class YoutubeApiClient : IYoutubeApiClient
{
    private readonly YoutubeClient _youtube;
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(10, 10);
    private readonly ILogger<YoutubeApiClient> _logger;

    public YoutubeApiClient(ILogger<YoutubeApiClient> logger)
    {
        _logger = logger;

        // OPTIMIZED HTTP HANDLER FOR REDUCED LATENCY
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5), // Increased for better connection reuse
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromSeconds(15) // Increased from 5s to 15s to handle network spikes
        };

        var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7");
        httpClient.DefaultRequestHeaders.Add("sec-ch-ua", "\"Not A(Brand\";v=\"99\", \"Google Chrome\";v=\"121\", \"Chromium\";v=\"121\"");
        httpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
        httpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");

        _youtube = new YoutubeClient(httpClient);
    }

    private async Task<T> ExecuteWithSemaphoreAsync<T>(Func<Task<T>> operation)
    {
        await _semaphore.WaitAsync();
        try { return await operation(); }
        finally { _semaphore.Release(); }
    }

    public Task<Video> GetVideoAsync(string videoIdOrUrl)
        => ExecuteWithSemaphoreAsync(() => _youtube.Videos.GetAsync(videoIdOrUrl).AsTask());

    public Task<StreamManifest> GetStreamManifestAsync(string videoId)
        => ExecuteWithSemaphoreAsync(() => _youtube.Videos.Streams.GetManifestAsync(videoId).AsTask());

    public Task<IReadOnlyList<VideoSearchResult>> SearchVideosAsync(string query, int count)
        => ExecuteWithSemaphoreAsync(() => _youtube.Search.GetVideosAsync(query).CollectAsync(count).AsTask());

    public Task<IReadOnlyList<PlaylistSearchResult>> SearchPlaylistsAsync(string query, int count)
        => ExecuteWithSemaphoreAsync(() => _youtube.Search.GetPlaylistsAsync(query).CollectAsync(count).AsTask());

    public Task<Channel> GetChannelAsync(string channelId)
        => ExecuteWithSemaphoreAsync(() => _youtube.Channels.GetAsync(channelId).AsTask());

    public Task<IReadOnlyList<PlaylistVideo>> GetChannelUploadsAsync(string channelId, int count = 50)
        => ExecuteWithSemaphoreAsync(() => _youtube.Channels.GetUploadsAsync(channelId).CollectAsync(count).AsTask());

    public Task<IReadOnlyList<PlaylistVideo>> GetPlaylistVideosAsync(string playlistId, int count)
        => ExecuteWithSemaphoreAsync(() => _youtube.Playlists.GetVideosAsync(playlistId).CollectAsync(count).AsTask());

    public Task<ClosedCaptionManifest> GetClosedCaptionManifestAsync(string videoId)
        => ExecuteWithSemaphoreAsync(() => _youtube.Videos.ClosedCaptions.GetManifestAsync(videoId).AsTask());

    public Task<ClosedCaptionTrack> GetClosedCaptionTrackAsync(ClosedCaptionTrackInfo trackInfo)
        => ExecuteWithSemaphoreAsync(() => _youtube.Videos.ClosedCaptions.GetAsync(trackInfo).AsTask());
}
