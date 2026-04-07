using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IGenreService
{
    Task<IEnumerable<GenreDto>> GetAllGenresAsync(CancellationToken ct = default);
    Task<GenreDto?> GetGenreByIdAsync(int id, CancellationToken ct = default);
    Task CreateGenreAsync(GenreDto genreDto, CancellationToken ct = default);
    Task UpdateGenreAsync(GenreDto genreDto, CancellationToken ct = default);
    Task DeleteGenreAsync(int id, CancellationToken ct = default);
    Task<GenreDetailsDto?> GetGenreByIdWithSongsAsync(int id, CancellationToken ct = default);
}
