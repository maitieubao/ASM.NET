using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IPlaylistService
{
    Task<PlaylistDto> CreatePlaylistAsync(int userId, string title, string? description, CancellationToken ct = default);
    Task<IEnumerable<PlaylistDto>> GetUserPlaylistsAsync(int userId, CancellationToken ct = default);
    Task<PlaylistDto?> GetPlaylistByIdAsync(int playlistId, int? userId = null, bool isAdmin = false, CancellationToken ct = default);
    Task AddSongToPlaylistAsync(int playlistId, int songId, int userId, bool isAdmin = false, CancellationToken ct = default);
    Task RemoveSongFromPlaylistAsync(int playlistId, int songId, int userId, bool isAdmin = false, CancellationToken ct = default);
    Task DeletePlaylistAsync(int playlistId, int userId, bool isAdmin = false, CancellationToken ct = default);
    
    // Featured Playlists
    Task<IEnumerable<PlaylistDto>> GetFeaturedPlaylistsAsync(CancellationToken ct = default);
    Task<PlaylistDto> CreateFeaturedPlaylistAsync(string title, string? featuredType, string? description, string? coverImageUrl, CancellationToken ct = default);
    Task UpdatePlaylistAsync(PlaylistDto dto, int userId, bool isAdmin = false, CancellationToken ct = default);
    Task<(IEnumerable<PlaylistDto> Playlists, int TotalCount)> GetPaginatedPlaylistsAsync(int page, int pageSize, string? searchTerm = null, CancellationToken ct = default);
    Task ReorderSongsAsync(int playlistId, List<int> sortedSongIds, int userId, bool isAdmin = false, CancellationToken ct = default);
}


