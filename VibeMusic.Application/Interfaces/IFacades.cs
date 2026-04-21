using VibeMusic.Application.DTOs;
using VibeMusic.Domain.Entities;

namespace VibeMusic.Application.Interfaces;

public interface IHomeFacade
{
    Task<HomeViewModel> BuildHomeViewModelAsync(int? userId, string? userName = null);
    Task<MusicSection?> GetHomeSectionAsync(string type, int? userId, bool refresh = false);
    Task<List<SearchResultDto>> SearchAllAsync(string query, int? userId);
    Task<IEnumerable<SongDto>> GetSongsByArtistAsync(string name);
    Task<IEnumerable<YoutubeVideoDetails>> GetDiscoverySongsAsync(string tag, int page, int limit);
    /// <summary>Lấy PlayCount thực từ DB theo SongId. Trả về 0 nếu không tìm thấy.</summary>
    Task<long> GetSongPlayCountAsync(int songId);
}

public interface IPlaybackFacade
{
    Task<PlaybackStreamDto> GetStreamAsync(string videoUrl, string? title, string? artist, int? userId, int? durationMs = null);
    Task<PlaybackStreamDto> ResolveAndGetStreamAsync(string query, string? title, string? artist, int? userId, int? durationMs = null);
    Task<RichMetadataDto> GetRichMetadataAsync(string videoId, string? lang = null);
}

public interface IProfileFacade
{
    Task<UserProfileViewModel> BuildUserProfileAsync(int userId);
}
