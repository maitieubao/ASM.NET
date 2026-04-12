using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Domain.Entities;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IHomeFacade
{
    Task<HomeViewModel> BuildHomeViewModelAsync(int? userId);
    Task<MusicSection?> GetHomeSectionAsync(string type, int? userId, bool refresh = false);
    Task<List<SearchResultDto>> SearchAllAsync(string query, int? userId);
    Task<IEnumerable<SongDto>> GetSongsByArtistAsync(string name);
}

public interface IPlaybackFacade
{
    Task<PlaybackStreamDto> GetStreamAsync(string videoUrl, string? title, string? artist, int? userId);
    Task<PlaybackStreamDto> ResolveAndGetStreamAsync(string query, string? title, string? artist, int? userId);
    Task<RichMetadataDto> GetRichMetadataAsync(string videoId, string? lang = null);
}

public interface IProfileFacade
{
    Task<UserProfileViewModel> BuildUserProfileAsync(int userId);
}
