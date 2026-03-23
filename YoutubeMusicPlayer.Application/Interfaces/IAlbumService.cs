using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IAlbumService
{
    Task<IEnumerable<AlbumDto>> GetAllAlbumsAsync();
    Task<AlbumDto?> GetAlbumByIdAsync(int id);
    Task CreateAlbumAsync(AlbumDto albumDto);
    Task UpdateAlbumAsync(AlbumDto albumDto);
    Task DeleteAlbumAsync(int id);
}
