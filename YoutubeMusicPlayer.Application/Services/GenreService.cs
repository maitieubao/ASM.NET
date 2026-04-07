using System.Threading;
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

    public async Task<IEnumerable<GenreDto>> GetAllGenresAsync(CancellationToken ct = default)
    {
        var genres = await _unitOfWork.Repository<Genre>().Query()
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync(ct);
        return genres.Select(MapToGenreDto);
    }

    public async Task<GenreDto?> GetGenreByIdAsync(int id, CancellationToken ct = default)
    {
        var g = await _unitOfWork.Repository<Genre>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.GenreId == id, ct);
        return g != null ? MapToGenreDto(g) : null;
    }

    public async Task<GenreDetailsDto?> GetGenreByIdWithSongsAsync(int id, CancellationToken ct = default)
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
            .FirstOrDefaultAsync(ct);

        return genreData;
    }

    public async Task CreateGenreAsync(GenreDto dto, CancellationToken ct = default)
    {
        // Optimized: Integrity Check - Prevent duplicate genre names
        var exists = await _unitOfWork.Repository<Genre>().Query()
            .AnyAsync(g => g.Name.ToLower() == dto.Name.ToLower(), ct);
        
        if (exists)
            throw new AppException($"Genre with name '{dto.Name}' already exists.");

        var genre = new Genre { Name = dto.Name, Description = dto.Description };
        await _unitOfWork.Repository<Genre>().AddAsync(genre, ct);
        await _unitOfWork.CompleteAsync(ct);
    }

    public async Task UpdateGenreAsync(GenreDto dto, CancellationToken ct = default)
    {
        var genre = await _unitOfWork.Repository<Genre>().GetByIdAsync(dto.GenreId, ct);
        if (genre == null) throw new AppException("Genre not found.");

        // Check for duplicate name if name changed
        if (!string.Equals(genre.Name, dto.Name, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _unitOfWork.Repository<Genre>().Query()
                .AnyAsync(g => g.Name.ToLower() == dto.Name.ToLower(), ct);
            
            if (exists)
                throw new AppException($"Genre with name '{dto.Name}' already exists.");
        }

        genre.Name = dto.Name;
        genre.Description = dto.Description;
        
        _unitOfWork.Repository<Genre>().Update(genre);
        await _unitOfWork.CompleteAsync(ct);
    }

    public async Task DeleteGenreAsync(int id, CancellationToken ct = default)
    {
        // Optimized: Integrity Guard - Check for associated songs before deletion to avoid DB exceptions
        var hasSongs = await _unitOfWork.Repository<SongGenre>().Query()
            .AnyAsync(sg => sg.GenreId == id, ct);
            
        if (hasSongs)
            throw new AppException("Cannot delete genre that contains songs. Please remove all songs from this genre first.");

        var genre = await _unitOfWork.Repository<Genre>().GetByIdAsync(id, ct);
        if (genre != null)
        {
            _unitOfWork.Repository<Genre>().Remove(genre);
            await _unitOfWork.CompleteAsync(ct);
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
