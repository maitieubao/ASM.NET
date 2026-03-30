using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    public async Task<PlaylistDto> CreatePlaylistAsync(int userId, string title, string? description)
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

        await _unitOfWork.Repository<Playlist>().AddAsync(playlist);
        await _unitOfWork.CompleteAsync();

        return MapToDto(playlist);
    }

    public async Task DeletePlaylistAsync(int playlistId, int? userId = null)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().GetByIdAsync(playlistId);
        if (playlist == null) return;
        
        if (userId != null && playlist.UserId != userId) return;

        var songsInPlaylist = await _unitOfWork.Repository<PlaylistSong>().FindAsync(ps => ps.PlaylistId == playlistId);
        foreach(var ps in songsInPlaylist)
            _unitOfWork.Repository<PlaylistSong>().Remove(ps);

        _unitOfWork.Repository<Playlist>().Remove(playlist);
        await _unitOfWork.CompleteAsync();
    }

    public async Task<PlaylistDto?> GetPlaylistByIdAsync(int playlistId, int? userId = null)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().GetByIdAsync(playlistId);
        if (playlist == null) return null;

        var songJoins = await _unitOfWork.Repository<PlaylistSong>().FindAsync(ps => ps.PlaylistId == playlistId);
        var songRelations = songJoins.OrderBy(ps => ps.Position).ToList();
        var songIds = songRelations.Select(ps => ps.SongId).ToList();

        // Get user likes if logged in
        var likedSongIds = new HashSet<int>();
        if (userId.HasValue)
        {
            var likes = await _unitOfWork.Repository<SongLike>().FindAsync(l => l.UserId == userId.Value);
            likedSongIds = new HashSet<int>(likes.Select(l => l.SongId));
        }

        var songs = new List<SongDto>();
        foreach (var sid in songIds)
        {
            var s = await _unitOfWork.Repository<Song>().GetByIdAsync(sid);
            if (s != null)
            {
                songs.Add(new SongDto
                {
                    SongId = s.SongId,
                    Title = s.Title,
                    YoutubeVideoId = s.YoutubeVideoId,
                    ThumbnailUrl = s.ThumbnailUrl,
                    Duration = s.Duration ?? 0,
                    PlayCount = s.PlayCount,
                    IsLiked = likedSongIds.Contains(s.SongId)
                });
            }
        }

        var dto = MapToDto(playlist);
        dto.SongIds = songIds;
        dto.Songs = songs;
        return dto;
    }

    public async Task<IEnumerable<PlaylistDto>> GetUserPlaylistsAsync(int userId)
    {
        var playlists = _unitOfWork.Repository<Playlist>()
            .Find(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        return playlists.Select(MapToDto);
    }

    public async Task AddSongToPlaylistAsync(int playlistId, int songId, int? userId = null)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().GetByIdAsync(playlistId);
        if (playlist == null) return;
        if (userId != null && playlist.UserId != userId) return;

        var existing = await _unitOfWork.Repository<PlaylistSong>()
            .FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId);

        if (existing == null)
        {
            // --- CALC NEXT POSITION ---
            var currentSongs = await _unitOfWork.Repository<PlaylistSong>().FindAsync(ps => ps.PlaylistId == playlistId);
            int nextPos = currentSongs.Any() ? currentSongs.Max(ps => ps.Position) + 1 : 1;

            await _unitOfWork.Repository<PlaylistSong>().AddAsync(new PlaylistSong
            {
                PlaylistId = playlistId,
                SongId = songId,
                AddedAt = DateTime.UtcNow,
                Position = nextPos
            });
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task RemoveSongFromPlaylistAsync(int playlistId, int songId, int? userId = null)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().GetByIdAsync(playlistId);
        if (playlist == null) return;
        if (userId != null && playlist.UserId != userId) return;

        var existing = await _unitOfWork.Repository<PlaylistSong>()
            .FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId);

        if (existing != null)
        {
            _unitOfWork.Repository<PlaylistSong>().Remove(existing);
            await _unitOfWork.CompleteAsync();
            
            // Re-normalize positions? (Optional, but good for Test Case 3.2 consistency)
            var remaining = await _unitOfWork.Repository<PlaylistSong>().FindAsync(ps => ps.PlaylistId == playlistId);
            var sorted = remaining.OrderBy(ps => ps.Position).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].Position = i + 1;
                _unitOfWork.Repository<PlaylistSong>().Update(sorted[i]);
            }
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task<IEnumerable<PlaylistDto>> GetFeaturedPlaylistsAsync()
    {
        var playlists = await _unitOfWork.Repository<Playlist>().FindAsync(p => p.IsFeatured);
        return playlists.Select(MapToDto);
    }

    public async Task<PlaylistDto> CreateFeaturedPlaylistAsync(string title, string? featuredType, string? description, string? coverImageUrl)
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

        await _unitOfWork.Repository<Playlist>().AddAsync(playlist);
        await _unitOfWork.CompleteAsync();

        return MapToDto(playlist);
    }

    public async Task UpdatePlaylistAsync(PlaylistDto dto)
    {
        var p = await _unitOfWork.Repository<Playlist>().GetByIdAsync(dto.PlaylistId);
        if (p != null)
        {
            p.Title = dto.Title;
            p.Description = dto.Description;
            p.CoverImageUrl = dto.CoverImageUrl;
            p.IsFeatured = dto.IsFeatured;
            p.FeaturedType = dto.FeaturedType;
            p.Visibility = dto.Visibility;
            _unitOfWork.Repository<Playlist>().Update(p);
            await _unitOfWork.CompleteAsync();
        }
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
