using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Videos.ClosedCaptions;
using YoutubeExplode.Search;
using YoutubeExplode.Channels;
using YoutubeExplode.Playlists;

namespace VibeMusic.Infrastructure.External;

public interface IYoutubeApiClient
{
    Task<Video> GetVideoAsync(string videoIdOrUrl);
    Task<StreamManifest> GetStreamManifestAsync(string videoId);
    Task<IReadOnlyList<VideoSearchResult>> SearchVideosAsync(string query, int count);
    Task<IReadOnlyList<PlaylistSearchResult>> SearchPlaylistsAsync(string query, int count);
    Task<Channel> GetChannelAsync(string channelId);
    Task<IReadOnlyList<PlaylistVideo>> GetChannelUploadsAsync(string channelId, int count = 50);
    Task<IReadOnlyList<PlaylistVideo>> GetPlaylistVideosAsync(string playlistId, int count);
    Task<ClosedCaptionManifest> GetClosedCaptionManifestAsync(string videoId);
    Task<ClosedCaptionTrack> GetClosedCaptionTrackAsync(ClosedCaptionTrackInfo trackInfo);
}
