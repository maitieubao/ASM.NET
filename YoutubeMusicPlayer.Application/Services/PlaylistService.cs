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
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Playlist>().AddAsync(playlist);
        await _unitOfWork.CompleteAsync();

        return new PlaylistDto 
        { 
            PlaylistId = playlist.PlaylistId, 
            UserId = playlist.UserId, 
            Title = playlist.Title, 
            Description = playlist.Description 
        };
    }

    public async Task DeletePlaylistAsync(int playlistId, int userId)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().GetByIdAsync(playlistId);
        if (playlist == null || playlist.UserId != userId) return;

        // Xóa các liên kết bài hát trước
        var songsInPlaylist = _unitOfWork.Repository<PlaylistSong>().Find(ps => ps.PlaylistId == playlistId).ToList();
        foreach(var ps in songsInPlaylist)
            _unitOfWork.Repository<PlaylistSong>().Remove(ps);

        _unitOfWork.Repository<Playlist>().Remove(playlist);
        await _unitOfWork.CompleteAsync();
    }

    public async Task<PlaylistDto?> GetPlaylistByIdAsync(int playlistId)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().GetByIdAsync(playlistId);
        if (playlist == null) return null;

        var songIds = _unitOfWork.Repository<PlaylistSong>()
            .Find(ps => ps.PlaylistId == playlistId)
            .OrderByDescending(ps => ps.AddedAt)
            .Select(ps => ps.SongId)
            .ToList();

        return new PlaylistDto
        {
            PlaylistId = playlist.PlaylistId,
            UserId = playlist.UserId,
            Title = playlist.Title,
            Description = playlist.Description,
            CoverImageUrl = playlist.CoverImageUrl,
            SongIds = songIds
        };
    }

    public async Task<IEnumerable<PlaylistDto>> GetUserPlaylistsAsync(int userId)
    {
        var playlists = _unitOfWork.Repository<Playlist>()
            .Find(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        var songJoins = _unitOfWork.Repository<PlaylistSong>()
            .Find(ps => playlists.Select(p => p.PlaylistId).Contains(ps.PlaylistId))
            .ToList();

        return await Task.FromResult(playlists.Select(p => new PlaylistDto
        {
            PlaylistId = p.PlaylistId,
            UserId = p.UserId,
            Title = p.Title,
            Description = p.Description,
            CoverImageUrl = p.CoverImageUrl,
            SongIds = songJoins.Where(sj => sj.PlaylistId == p.PlaylistId).Select(sj => sj.SongId)
        }));
    }

    public async Task AddSongToPlaylistAsync(int playlistId, int songId, int userId)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().GetByIdAsync(playlistId);
        if (playlist == null || playlist.UserId != userId) return; // Không có quyền

        var existing = await _unitOfWork.Repository<PlaylistSong>()
            .FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId);

        if (existing == null)
        {
            await _unitOfWork.Repository<PlaylistSong>().AddAsync(new PlaylistSong
            {
                PlaylistId = playlistId,
                SongId = songId,
                AddedAt = DateTime.UtcNow
            });
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task RemoveSongFromPlaylistAsync(int playlistId, int songId, int userId)
    {
        var playlist = await _unitOfWork.Repository<Playlist>().GetByIdAsync(playlistId);
        if (playlist == null || playlist.UserId != userId) return;

        var existing = await _unitOfWork.Repository<PlaylistSong>()
            .FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId);

        if (existing != null)
        {
            _unitOfWork.Repository<PlaylistSong>().Remove(existing);
            await _unitOfWork.CompleteAsync();
        }
    }
}
