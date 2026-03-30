using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
        return genres.Select(g => new GenreDto { GenreId = g.GenreId, Name = g.Name, Description = g.Description });
    }

    public async Task<GenreDto?> GetGenreByIdAsync(int id)
    {
        var g = await _unitOfWork.Repository<Genre>().GetByIdAsync(id);
        if (g == null) return null;
        return new GenreDto { GenreId = g.GenreId, Name = g.Name, Description = g.Description };
    }

    public async Task<GenreDetailsDto?> GetGenreByIdWithSongsAsync(int id)
    {
        var g = await _unitOfWork.Repository<Genre>().GetByIdAsync(id);
        if (g == null) return null;

        var songIds = await _unitOfWork.Repository<SongGenre>().Query()
            .Where(sg => sg.GenreId == id)
            .Select(sg => sg.SongId)
            .ToListAsync();

        var songs = await _unitOfWork.Repository<Song>().Query()
            .Where(s => songIds.Contains(s.SongId))
            .OrderByDescending(s => s.PlayCount)
            .Take(50)
            .ToListAsync();

        return new GenreDetailsDto
        {
            GenreId = g.GenreId,
            Name = g.Name,
            Description = g.Description,
            Songs = songs.Select(s => new SongDto
            {
                SongId = s.SongId,
                Title = s.Title,
                YoutubeVideoId = s.YoutubeVideoId,
                ThumbnailUrl = s.ThumbnailUrl,
                Duration = s.Duration,
                IsExplicit = s.IsExplicit,
                IsPremiumOnly = s.IsPremiumOnly,
                PlayCount = s.PlayCount
                // AuthorName can be fetched if needed, but for simplicity:
            })
        };
    }

    public async Task CreateGenreAsync(GenreDto dto)
    {
        var genre = new Genre { Name = dto.Name, Description = dto.Description };
        await _unitOfWork.Repository<Genre>().AddAsync(genre);
        await _unitOfWork.CompleteAsync();
    }

    public async Task UpdateGenreAsync(GenreDto dto)
    {
        var genre = await _unitOfWork.Repository<Genre>().GetByIdAsync(dto.GenreId);
        if (genre != null)
        {
            genre.Name = dto.Name;
            genre.Description = dto.Description;
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
