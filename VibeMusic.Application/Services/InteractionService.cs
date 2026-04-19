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
        // Optimized: Group by SongId to get the latest interaction per song, then order and take
        return await _unitOfWork.Repository<ListeningHistory>().Query()
            .AsNoTracking()
            .Where(lh => lh.UserId == userId)
            .GroupBy(lh => lh.SongId)
            .Select(g => new { SongId = g.Key, MaxDate = g.Max(lh => lh.ListenedAt) })
            .OrderByDescending(x => x.MaxDate)
            .Take(count)
            .Select(x => x.SongId)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetRecentSearchHistoryAsync(int userId, int count = 10)
    {
        // Optimized: Group by Query to get the latest search date per query string
        return await _unitOfWork.Repository<UserSearchHistory>().Query()
            .AsNoTracking()
            .Where(sh => sh.UserId == userId)
            .GroupBy(sh => sh.SearchQuery)
            .Select(g => new { Query = g.Key, MaxDate = g.Max(sh => sh.SearchedAt) })
            .OrderByDescending(x => x.MaxDate)
            .Take(count)
            .Select(x => x.Query)
            .ToListAsync();
    }

    // --- Advanced A+ Tracking Implementation ---

    public async Task UpdateListeningStatsAsync(int userId, int songId, double durationSeconds)
    {
        if (durationSeconds <= 0) return;

        try
        {
            // 1. Update User Global Stats
            var user = await _unitOfWork.Repository<User>().Query()
                .FirstOrDefaultAsync(u => u.UserId == userId);
                
            if (user != null)
            {
                user.TotalListenSeconds += durationSeconds;
                _unitOfWork.Repository<User>().Update(user);
            }

            // 2. Fetch song with includes (Using AsNoTracking for the initial check to see if it exists)
            var song = await _unitOfWork.Repository<Song>().Query()
                .Include(s => s.SongArtists)
                .Include(s => s.SongGenres).ThenInclude(sg => sg.Genre)
                .FirstOrDefaultAsync(s => s.SongId == songId);

            if (song == null) return;

            // 3. Update Song PlayCount (REMOVED: Now handled by IncrementPlayCountAsync for accuracy)
            // Lượt xem được đếm riêng qua RecordView API để đạt chuẩn 30s/50%


            // 4. Update Genre-specific Stats
            foreach (var sg in song.SongGenres)
            {
                var genreName = sg.Genre.Name;
                
                var stat = await _unitOfWork.Repository<UserGenreStat>().Query()
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.GenreName == genreName);

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

            await _unitOfWork.CompleteAsync();
        }
        catch (ObjectDisposedException)
        {
            // Log and absorb if the context was disposed during a shutdown/retry cycle
            throw; 
        }
    }

    public async Task IncrementPlayCountAsync(int songId)
    {
        try
        {
            var song = await _unitOfWork.Repository<Song>().Query()
                .FirstOrDefaultAsync(s => s.SongId == songId);

            if (song != null)
            {
                song.PlayCount += 1;
                _unitOfWork.Repository<Song>().Update(song);
                await _unitOfWork.CompleteAsync();
                
                // DETAILED LOG FOR VERIFICATION
                System.Console.WriteLine($"[DB-UPDATE] Song #{songId} ({song.Title}) -> New PlayCount: {song.PlayCount}");
            }
            else
            {
                System.Console.WriteLine($"[DB-UPDATE] WARNING: Song #{songId} not found. Cannot increment play count.");
            }
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetTopPreferredGenresAsync(int userId, int count = 5)
    {
        // Optimized: SQL-level Take and Select instead of RAM processing
        return await _unitOfWork.Repository<UserGenreStat>().Query()
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.ListenSeconds)
            .Take(count)
            .Select(s => s.GenreName)
            .ToListAsync();
    }

    public async Task<Dictionary<string, double>> GetTopPreferredGenresWithWeightsAsync(int userId, int count = 5)
    {
        // Optimized: SQL-level sorting and taking
        var stats = await _unitOfWork.Repository<UserGenreStat>().Query()
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.ListenSeconds)
            .Take(count)
            .ToListAsync();

        return stats.ToDictionary(s => s.GenreName, s => s.ListenSeconds);
    }

    public async Task<IEnumerable<string>> GetTopPreferredArtistsAsync(int userId, int count = 10)
    {
        // Optimized: SQL-level grouping and counting to avoid RAM bloat
        return await _unitOfWork.Repository<ListeningHistory>().Query()
            .AsNoTracking()
            .Where(lh => lh.UserId == userId)
            .SelectMany(lh => lh.Song.SongArtists)
            .GroupBy(sa => sa.Artist.Name)
            .OrderByDescending(g => g.Count())
            .Take(count)
            .Select(g => g.Key)
            .ToListAsync();
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
        // Optimized: Get unique video IDs ordered by latest interaction date
        return await _unitOfWork.Repository<ListeningHistory>().Query()
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .GroupBy(h => h.Song.YoutubeVideoId)
            .Select(g => new { VideoId = g.Key, MaxDate = g.Max(h => h.ListenedAt) })
            .OrderByDescending(x => x.MaxDate)
            .Take(count)
            .Select(x => x.VideoId)
            .ToListAsync();
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
        return await _unitOfWork.Repository<SongLike>().Query()
            .AnyAsync(l => l.UserId == userId && l.SongId == songId);
    }

    public async Task<IEnumerable<int>> GetLikedSongIdsAsync(int userId)
    {
        // Optimized: Query to avoid loading list into RAM before projecting
        return await _unitOfWork.Repository<SongLike>().Query()
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.LikedAt)
            .Select(l => l.SongId)
            .ToListAsync();
    }

    public async Task<(IEnumerable<int> Ids, int TotalCount)> GetLikedSongIdsPaginatedAsync(int userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<SongLike>().Query()
            .AsNoTracking()
            .Where(l => l.UserId == userId);

        int totalCount = await query.CountAsync(ct);
        var ids = await query.OrderByDescending(l => l.LikedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => l.SongId)
            .ToListAsync(ct);

        return (ids, totalCount);
    }

    public async Task<int> GetLikeCountAsync(int songId)
    {
        return await _unitOfWork.Repository<SongLike>().Query()
            .CountAsync(l => l.SongId == songId);
    }
}
