using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IPlaylistService
{
    Task<PlaylistDto> CreatePlaylistAsync(int userId, string title, string? description);
    Task<IEnumerable<PlaylistDto>> GetUserPlaylistsAsync(int userId);
    Task<PlaylistDto?> GetPlaylistByIdAsync(int playlistId, int? userId = null);
    Task AddSongToPlaylistAsync(int playlistId, int songId, int? userId = null);
    Task RemoveSongFromPlaylistAsync(int playlistId, int songId, int? userId = null);
    Task DeletePlaylistAsync(int playlistId, int? userId = null);
    
    // Featured Playlists
    Task<IEnumerable<PlaylistDto>> GetFeaturedPlaylistsAsync();
    Task<PlaylistDto> CreateFeaturedPlaylistAsync(string title, string? featuredType, string? description, string? coverImageUrl);
    Task UpdatePlaylistAsync(PlaylistDto dto);
}


