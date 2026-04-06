using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.Common;
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
        return genres.Select(MapToGenreDto);
    }

    public async Task<GenreDto?> GetGenreByIdAsync(int id)
    {
        var g = await _unitOfWork.Repository<Genre>().GetByIdAsync(id);
        return g != null ? MapToGenreDto(g) : null;
    }

    public async Task<GenreDetailsDto?> GetGenreByIdWithSongsAsync(int id)
    {
        // Optimized: Single database trip using projection to avoid N+1 and loading full song entities into RAM
        var genreData = await _unitOfWork.Repository<Genre>().Query()
            .AsNoTracking()
            .Where(g => g.GenreId == id)
            .Select(g => new GenreDetailsDto
            {
                GenreId = g.GenreId,
                Name = g.Name,
                Description = g.Description,
                Songs = _unitOfWork.Repository<SongGenre>().Query()
                    .Where(sg => sg.GenreId == id)
                    .OrderByDescending(sg => sg.Song.PlayCount)
                    .Take(50)
                    .Select(sg => new SongDto
                    {
                        SongId = sg.Song.SongId,
                        Title = sg.Song.Title,
                        YoutubeVideoId = sg.Song.YoutubeVideoId,
                        ThumbnailUrl = sg.Song.ThumbnailUrl,
                        Duration = sg.Song.Duration,
                        IsExplicit = sg.Song.IsExplicit,
                        IsPremiumOnly = sg.Song.IsPremiumOnly,
                        PlayCount = sg.Song.PlayCount
                    }).ToList()
            })
            .FirstOrDefaultAsync();

        return genreData;
    }

    public async Task CreateGenreAsync(GenreDto dto)
    {
        // Optimized: Integrity Check - Prevent duplicate genre names
        var exists = await _unitOfWork.Repository<Genre>().Query()
            .AnyAsync(g => g.Name.ToLower() == dto.Name.ToLower());
        
        if (exists)
            throw new AppException($"Genre with name '{dto.Name}' already exists.");

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
        // Optimized: Integrity Guard - Check for associated songs before deletion to avoid DB exceptions
        var hasSongs = await _unitOfWork.Repository<SongGenre>().Query()
            .AnyAsync(sg => sg.GenreId == id);
            
        if (hasSongs)
            throw new AppException("Cannot delete genre that contains songs. Please remove all songs from this genre first.");

        var genre = await _unitOfWork.Repository<Genre>().GetByIdAsync(id);
        if (genre != null)
        {
            _unitOfWork.Repository<Genre>().Remove(genre);
            await _unitOfWork.CompleteAsync();
        }
    }

    private GenreDto MapToGenreDto(Genre g)
    {
        return new GenreDto
        {
            GenreId = g.GenreId,
            Name = g.Name,
            Description = g.Description
        };
    }
}
