using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IPlaylistService
{
    Task<PlaylistDto> CreatePlaylistAsync(int userId, string title, string? description);
    Task<IEnumerable<PlaylistDto>> GetUserPlaylistsAsync(int userId);
    Task<PlaylistDto?> GetPlaylistByIdAsync(int playlistId);
    Task AddSongToPlaylistAsync(int playlistId, int songId, int userId);
    Task RemoveSongFromPlaylistAsync(int playlistId, int songId, int userId);
    Task DeletePlaylistAsync(int playlistId, int userId);
}


