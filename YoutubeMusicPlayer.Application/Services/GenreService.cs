using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class GenreService : IGenreService
{
    private readonly IUnitOfWork _unitOfWork;

    public GenreService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<GenreDto>> GetAllGenresAsync()
    {
        var genres = await _unitOfWork.Repository<Genre>().GetAllAsync();
        return genres.Select(g => new GenreDto { GenreId = g.GenreId, Name = g.Name });
    }

    public async Task<GenreDto?> GetGenreByIdAsync(int id)
    {
        var g = await _unitOfWork.Repository<Genre>().GetByIdAsync(id);
        if (g == null) return null;
        return new GenreDto { GenreId = g.GenreId, Name = g.Name };
    }

    public async Task CreateGenreAsync(GenreDto dto)
    {
        var genre = new Genre { Name = dto.Name };
        await _unitOfWork.Repository<Genre>().AddAsync(genre);
        await _unitOfWork.CompleteAsync();
    }

    public async Task UpdateGenreAsync(GenreDto dto)
    {
        var genre = await _unitOfWork.Repository<Genre>().GetByIdAsync(dto.GenreId);
        if (genre != null)
        {
            genre.Name = dto.Name;
            _unitOfWork.Repository<Genre>().Update(genre);
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task DeleteGenreAsync(int id)
    {
        var genre = await _unitOfWork.Repository<Genre>().GetByIdAsync(id);
        if (genre != null)
        {
            _unitOfWork.Repository<Genre>().Remove(genre);
            await _unitOfWork.CompleteAsync();
        }
    }
}
