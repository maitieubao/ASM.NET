using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IGenreService
{
    Task<IEnumerable<GenreDto>> GetAllGenresAsync();
    Task<GenreDto?> GetGenreByIdAsync(int id);
    Task CreateGenreAsync(GenreDto genreDto);
    Task UpdateGenreAsync(GenreDto genreDto);
    Task DeleteGenreAsync(int id);
}
