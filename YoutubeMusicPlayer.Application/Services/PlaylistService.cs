using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class PlaylistService : IPlaylistService
{
    private readonly IUnitOfWork _unitOfWork;

    public PlaylistService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PlaylistDto> CreatePlaylistAsync(int userId, string title, string? description, CancellationToken ct = default)
    {
        var playlist = new Playlist
        {
            UserId = userId,
            Title = title,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            IsFeatured = false,
            Visibility = "Public"
        };

        await _unitOfWork.Repository<Playlist>().AddAsync(playlist, ct);
        await _unitOfWork.CompleteAsync(ct);

        return MapToDto(playlist);
    }

    public async Task DeletePlaylistAsync(int playlistId, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().Query()
            .FirstOrDefaultAsync(p => p.PlaylistId == playlistId && !p.IsDeleted, ct);
        
        if (playlist == null) return;
        if (playlist.UserId != userId && !isAdmin) throw new UnauthorizedAccessException("Bạn không có quyền xóa danh sách phát này.");

        playlist.IsDeleted = true;
        _unitOfWork.Repository<Playlist>().Update(playlist);
        await _unitOfWork.CompleteAsync(ct);
    }

    public async Task<PlaylistDto?> GetPlaylistByIdAsync(int playlistId, int? userId = null, bool isAdmin = false, CancellationToken ct = default)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlaylistId == playlistId && !p.IsDeleted, ct);

        if (playlist == null) return null;
        
        // Ownership check for private playlists
        if (userId.HasValue && playlist.UserId != userId.Value && !isAdmin && playlist.Visibility == "Private")
            throw new UnauthorizedAccessException("Bạn không có quyền xem danh sách phát này.");

        // Optimized: Single SQL Trip with Projection and Joined ordering
        return await _unitOfWork.Repository<Playlist>().Query()
            .AsNoTracking()
            .Where(p => p.PlaylistId == playlistId && !p.IsDeleted)
            .Select(p => new PlaylistDto
            {
                PlaylistId = p.PlaylistId,
                UserId = p.UserId,
                Title = p.Title,
                Description = p.Description,
                CoverImageUrl = p.CoverImageUrl,
                IsFeatured = p.IsFeatured,
                Visibility = p.Visibility,
                // Nested Projection: Order and Map songs directly in SQL
                Songs = p.PlaylistSongs
                    .OrderBy(ps => ps.Position)
                    .Select(ps => new SongDto
                    {
                        SongId = ps.Song.SongId,
                        Title = ps.Song.Title,
                        YoutubeVideoId = ps.Song.YoutubeVideoId,
                        ThumbnailUrl = ps.Song.ThumbnailUrl,
                        Duration = ps.Song.Duration ?? 0,
                        PlayCount = ps.Song.PlayCount,
                        // Optimized: Inline Like status check
                        IsLiked = userId.HasValue && ps.Song.SongLikes.Any(l => l.UserId == userId.Value)
                    }).ToList(),
                SongIds = p.PlaylistSongs.OrderBy(ps => ps.Position).Select(ps => ps.SongId).ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<PlaylistDto>> GetUserPlaylistsAsync(int userId, CancellationToken ct = default)
    {
        // Optimized: SQL-level ordering and AsNoTracking for pure read
        return await _unitOfWork.Repository<Playlist>().Query()
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PlaylistDto
            {
                PlaylistId = p.PlaylistId,
                UserId = p.UserId,
                Title = p.Title,
                Description = p.Description,
                CoverImageUrl = p.CoverImageUrl,
                IsFeatured = p.IsFeatured,
                Visibility = p.Visibility
            })
            .ToListAsync(ct);
    }

    public async Task AddSongToPlaylistAsync(int playlistId, int songId, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().Query()
            .FirstOrDefaultAsync(p => p.PlaylistId == playlistId && !p.IsDeleted, ct);

        if (playlist == null) return;
        if (playlist.UserId != userId && !isAdmin) throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa danh sách phát này.");

        var existing = await _unitOfWork.Repository<PlaylistSong>()
            .FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId, ct);

        if (existing == null)
        {
            // Optimized: Calculate next position in a single trip
            var nextPos = await _unitOfWork.Repository<PlaylistSong>().Query()
                .Where(ps => ps.PlaylistId == playlistId)
                .Select(ps => (int?)ps.Position)
                .MaxAsync(ct) ?? 0;

            await _unitOfWork.Repository<PlaylistSong>().AddAsync(new PlaylistSong
            {
                PlaylistId = playlistId,
                SongId = songId,
                AddedAt = DateTime.UtcNow,
                Position = nextPos + 1
            }, ct);
            await _unitOfWork.CompleteAsync(ct);
        }
    }

    public async Task RemoveSongFromPlaylistAsync(int playlistId, int songId, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().Query()
            .AsNoTracking() // Just checking existence/ownership
            .FirstOrDefaultAsync(p => p.PlaylistId == playlistId && !p.IsDeleted, ct);
            
        if (playlist == null) return;
        if (playlist.UserId != userId && !isAdmin) throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa danh sách phát này.");

        var existing = await _unitOfWork.Repository<PlaylistSong>()
            .FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId, ct);

        if (existing != null)
        {
            int deletedPos = existing.Position;
            _unitOfWork.Repository<PlaylistSong>().Remove(existing);
            await _unitOfWork.CompleteAsync(ct);
            
            // Optimized: Bulk Re-indexing - Single SQL Command instead of loop of N Updates
            await _unitOfWork.ExecuteSqlRawAsync(
                "UPDATE playlistsongs SET position = position - 1 WHERE playlistid = {0} AND position > {1}", 
                ct, playlistId, deletedPos);
        }
    }

    public async Task<IEnumerable<PlaylistDto>> GetFeaturedPlaylistsAsync(CancellationToken ct = default)
    {
        // Optimized: Shifted from Find (IEnumerable) to SQL Query to avoid RAM bloat
        return await _unitOfWork.Repository<Playlist>().Query()
            .AsNoTracking()
            .Where(p => p.IsFeatured && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt) // Standardize order
            .Select(p => new PlaylistDto
            {
                PlaylistId = p.PlaylistId,
                Title = p.Title,
                Description = p.Description,
                CoverImageUrl = p.CoverImageUrl,
                IsFeatured = true,
                FeaturedType = p.FeaturedType,
                Visibility = p.Visibility
            })
            .ToListAsync(ct);
    }

    public async Task<PlaylistDto> CreateFeaturedPlaylistAsync(string title, string? featuredType, string? description, string? coverImageUrl, CancellationToken ct = default)
    {
        var playlist = new Playlist
        {
            Title = title,
            Description = description,
            CoverImageUrl = coverImageUrl,
            IsFeatured = true,
            FeaturedType = featuredType,
            CreatedAt = DateTime.UtcNow,
            UserId = null,
            Visibility = "Public"
        };

        await _unitOfWork.Repository<Playlist>().AddAsync(playlist, ct);
        await _unitOfWork.CompleteAsync(ct);

        return MapToDto(playlist);
    }

    public async Task UpdatePlaylistAsync(PlaylistDto dto, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        var p = await _unitOfWork.Repository<Playlist>().GetByIdAsync(dto.PlaylistId, ct);
        if (p == null || p.IsDeleted) return;
        
        if (p.UserId != userId && !isAdmin) throw new UnauthorizedAccessException("Bạn không có quyền cập nhật danh sách phát này.");

        p.Title = dto.Title;
        p.Description = dto.Description;
        p.CoverImageUrl = dto.CoverImageUrl;
        p.IsFeatured = dto.IsFeatured;
        p.FeaturedType = dto.FeaturedType;
        p.Visibility = dto.Visibility;
        _unitOfWork.Repository<Playlist>().Update(p);
        await _unitOfWork.CompleteAsync(ct);
    }

    public async Task<(IEnumerable<PlaylistDto> Playlists, int TotalCount)> GetPaginatedPlaylistsAsync(int page, int pageSize, string? searchTerm = null, CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<Playlist>().Query()
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(p => p.Title.Contains(searchTerm) || (p.Description != null && p.Description.Contains(searchTerm)));
        }

        int totalCount = await query.CountAsync(ct);
        var playlists = await query.OrderByDescending(p => p.CreatedAt)
                                   .Skip((page - 1) * pageSize)
                                   .Select(p => new PlaylistDto
                                   {
                                        PlaylistId = p.PlaylistId,
                                        Title = p.Title,
                                        Description = p.Description,
                                        CoverImageUrl = p.CoverImageUrl,
                                        IsFeatured = p.IsFeatured,
                                        Visibility = p.Visibility
                                   })
                                   .ToListAsync(ct);

        return (playlists, totalCount);
    }

    private PlaylistDto MapToDto(Playlist p)
    {
        return new PlaylistDto
        {
            PlaylistId = p.PlaylistId,
            UserId = p.UserId,
            Title = p.Title,
            Description = p.Description,
            CoverImageUrl = p.CoverImageUrl,
            IsFeatured = p.IsFeatured,
            FeaturedType = p.FeaturedType,
            Visibility = p.Visibility ?? "Public"
        };
    }
}
