using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IAlbumService
{
    Task<IEnumerable<AlbumDto>> GetAllAlbumsAsync(CancellationToken ct = default);
    Task<(IEnumerable<AlbumDto> Albums, int TotalCount)> GetPaginatedAlbumsAsync(int page, int pageSize, string? searchTerm = null, CancellationToken ct = default);
    Task<AlbumDto?> GetAlbumByIdAsync(int id, CancellationToken ct = default);
    Task CreateAlbumAsync(AlbumDto albumDto, CancellationToken ct = default);
    Task UpdateAlbumAsync(AlbumDto albumDto, CancellationToken ct = default);
    Task DeleteAlbumAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<AlbumDto>> GetRecentAlbumsAsync(int count, CancellationToken ct = default);
    Task<IEnumerable<AlbumDto>> SearchAlbumsAsync(string query, CancellationToken ct = default);
    Task<IEnumerable<AlbumDto>> GetTrendingAlbumsAsync(int count, CancellationToken ct = default);
    Task EnsureAlbumSyncMetadataBackground(int albumId, string? artistName, CancellationToken ct = default);
}
