using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface ISongService
{
    Task<IEnumerable<SongDto>> GetAllSongsAsync();
    Task<(IEnumerable<SongDto> Songs, int TotalCount)> GetPaginatedSongsAsync(int page, int pageSize, string? searchTerm = null);
    Task<SongDto?> GetSongByIdAsync(int id);
    Task CreateSongAsync(SongDto songDto);
    Task ImportFromYoutubeAsync(string videoUrl);
    Task<SongDto?> GetOrCreateByYoutubeIdAsync(string youtubeId);
    Task UpdateSongAsync(SongDto songDto);
    Task DeleteSongAsync(int id);
    Task<SongDto?> ImportAndReturnSongAsync(string videoUrl);
    Task<Dictionary<string, long>> GetUniversalPlayCountsAsync();
    Task<IEnumerable<SongDto>> GetTrendingSongsAsync(int count = 10);
    Task<IEnumerable<SongDto>> GetSongsByIdsAsync(IEnumerable<int> ids);
}
