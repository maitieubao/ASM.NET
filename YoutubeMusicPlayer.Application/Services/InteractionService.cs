using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class InteractionService : IInteractionService
{
    private readonly IUnitOfWork _unitOfWork;

    public InteractionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<int>> GetRecentListeningHistoryAsync(int userId, int count = 20)
    {
        return await _unitOfWork.Repository<ListeningHistory>()
            .Find(lh => lh.UserId == userId)
            .OrderByDescending(lh => lh.ListenedAt)
            .Select(lh => lh.SongId)
            .Distinct()
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetRecentSearchHistoryAsync(int userId, int count = 10)
    {
        return await _unitOfWork.Repository<UserSearchHistory>()
            .Find(sh => sh.UserId == userId)
            .OrderByDescending(sh => sh.SearchedAt)
            .Select(sh => sh.SearchQuery)
            .Distinct()
            .Take(count)
            .ToListAsync();
    }

    // --- Advanced A+ Tracking Implementation ---

    public async Task UpdateListeningStatsAsync(int userId, int songId, double durationSeconds)
    {
        if (durationSeconds <= 0) return;

        // 1. Update User Global Stats
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId);
        if (user != null)
        {
            user.TotalListenSeconds += durationSeconds;
            _unitOfWork.Repository<User>().Update(user);
        }

        // 2. Update Song PlayCount (Only if play is > 1 min - A+ Quality filter)
        if (durationSeconds > 60)
        {
            var song = await _unitOfWork.Repository<Song>().GetByIdAsync(songId);
            if (song != null)
            {
                song.PlayCount += 1;
                _unitOfWork.Repository<Song>().Update(song);
            }
        }

        // 3. Update Genre-specific Stats (De-aggregate genres from the song)
        var songData = _unitOfWork.Repository<Song>().Query()
            .Include(s => s.SongGenres)
            .ThenInclude(sg => sg.Genre)
            .FirstOrDefault(s => s.SongId == songId);
        if (songData != null)
        {
            foreach (var sg in songData.SongGenres)
            {
                var genreName = sg.Genre.Name;
                var stat = _unitOfWork.Repository<UserGenreStat>()
                    .Find(s => s.UserId == userId && s.GenreName == genreName)
                    .FirstOrDefault();

                if (stat == null)
                {
                    stat = new UserGenreStat
                    {
                        UserId = userId,
                        GenreName = genreName,
                        ListenSeconds = durationSeconds,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<UserGenreStat>().AddAsync(stat);
                }
                else
                {
                    stat.ListenSeconds += durationSeconds;
                    stat.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.Repository<UserGenreStat>().Update(stat);
                }
            }
        }

        await _unitOfWork.CompleteAsync();
    }

    public async Task<IEnumerable<string>> GetTopPreferredGenresAsync(int userId, int count = 5)
    {
        var stats = _unitOfWork.Repository<UserGenreStat>()
            .Find(s => s.UserId == userId)
            .OrderByDescending(s => s.ListenSeconds)
            .Take(count)
            .Select(s => s.GenreName)
            .ToList();

        return await Task.FromResult(stats);
    }

    public async Task<Dictionary<string, double>> GetTopPreferredGenresWithWeightsAsync(int userId, int count = 5)
    {
        var stats = _unitOfWork.Repository<UserGenreStat>()
            .Find(s => s.UserId == userId)
            .OrderByDescending(s => s.ListenSeconds)
            .Take(count)
            .ToDictionary(s => s.GenreName, s => s.ListenSeconds);

        return await Task.FromResult(stats);
    }

    public async Task RecordListeningHistoryAsync(int userId, int songId)
    {
        var history = new ListeningHistory
        {
            UserId = userId,
            SongId = songId,
            ListenedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<ListeningHistory>().AddAsync(history);
        await _unitOfWork.CompleteAsync();
    }

    public async Task RecordSearchHistoryAsync(int userId, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        var history = new UserSearchHistory
        {
            UserId = userId,
            SearchQuery = query.Trim(),
            SearchedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<UserSearchHistory>().AddAsync(history);
        await _unitOfWork.CompleteAsync();
    }

    public async Task<List<string>> GetHistoryVideoIdsAsync(int userId, int count = 50)
    {
        var history = await _unitOfWork.Repository<ListeningHistory>().Query()
            .Include(h => h.Song)
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ListenedAt)
            .Take(count)
            .ToListAsync();

        return history.Select(h => h.Song.YoutubeVideoId).Distinct().ToList();
    }

    public async Task<bool> ToggleLikeAsync(int userId, int songId)
    {
        var existing = await _unitOfWork.Repository<SongLike>()
            .FirstOrDefaultAsync(l => l.UserId == userId && l.SongId == songId);

        if (existing != null)
        {
            _unitOfWork.Repository<SongLike>().Remove(existing);
            await _unitOfWork.CompleteAsync();
            return false; // Result is false (unliked)
        }
        else
        {
            var like = new SongLike { UserId = userId, SongId = songId, LikedAt = DateTime.UtcNow };
            await _unitOfWork.Repository<SongLike>().AddAsync(like);
            await _unitOfWork.CompleteAsync();
            return true; // Result is true (liked)
        }
    }

    public async Task<bool> IsSongLikedAsync(int userId, int songId)
    {
        var like = await _unitOfWork.Repository<SongLike>()
            .FirstOrDefaultAsync(l => l.UserId == userId && l.SongId == songId);
        return like != null;
    }

    public async Task<IEnumerable<int>> GetLikedSongIdsAsync(int userId)
    {
        var likes = await _unitOfWork.Repository<SongLike>().FindAsync(l => l.UserId == userId);
        return likes.Select(l => l.SongId);
    }
}
