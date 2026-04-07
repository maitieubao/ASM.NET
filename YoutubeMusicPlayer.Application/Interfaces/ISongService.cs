using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface ISongService
{
    Task<IEnumerable<SongDto>> GetAllSongsAsync(CancellationToken ct = default);
    Task<(IEnumerable<SongDto> Songs, int TotalCount)> GetPaginatedSongsAsync(int page, int pageSize, string? searchTerm = null, CancellationToken ct = default);
    Task<SongDto?> GetSongByIdAsync(int id, CancellationToken ct = default);
    Task CreateSongAsync(SongDto songDto, CancellationToken ct = default);
    Task ImportFromYoutubeAsync(string videoUrl, CancellationToken ct = default);
    Task<SongDto?> GetOrCreateByYoutubeIdAsync(string youtubeId, CancellationToken ct = default);
    Task UpdateSongAsync(SongDto songDto, CancellationToken ct = default);
    Task DeleteSongAsync(int id, CancellationToken ct = default);
    Task<SongDto?> ImportAndReturnSongAsync(string videoUrl, CancellationToken ct = default);
    Task<Dictionary<string, long>> GetUniversalPlayCountsAsync(CancellationToken ct = default);
    Task<IEnumerable<SongDto>> GetTrendingSongsAsync(int count = 10, CancellationToken ct = default);
    Task<IEnumerable<SongDto>> GetSongsByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);
    Task<(IEnumerable<SongDto> Songs, int TotalCount)> GetSongsByIdsPaginatedAsync(IEnumerable<int> ids, int page, int pageSize, CancellationToken ct = default);
    Task EnrichSongAsync(int songId, CancellationToken ct = default);
    Task<bool> TogglePremiumStatusAsync(int id, CancellationToken ct = default);
    Task<bool> ToggleExplicitStatusAsync(int id, CancellationToken ct = default);
}
