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
        // Optimized: SQL-level processing to avoid loading all history into RAM
        return await _unitOfWork.Repository<ListeningHistory>().Query()
            .AsNoTracking()
            .Where(lh => lh.UserId == userId)
            .OrderByDescending(lh => lh.ListenedAt)
            .Select(lh => lh.SongId)
            .Distinct()
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetRecentSearchHistoryAsync(int userId, int count = 10)
    {
        // Optimized: SQL-level processing to avoid loading all searches into RAM
        return await _unitOfWork.Repository<UserSearchHistory>().Query()
            .AsNoTracking()
            .Where(sh => sh.UserId == userId)
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

        // 2. Fetch all necessary song, artist, and genre data in ONE query
        var song = await _unitOfWork.Repository<Song>().Query()
            .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
            .Include(s => s.SongGenres).ThenInclude(sg => sg.Genre)
            .FirstOrDefaultAsync(s => s.SongId == songId);

        if (song == null) return;

        // 3. Update Song PlayCount (Only if play is > 30s)
        if (durationSeconds > 30)
        {
            song.PlayCount += 1;
            _unitOfWork.Repository<Song>().Update(song);

            // 3.1 Update Artist Popularity (Implicitly if logic needed later, but removed redundant Update check)
            // Artist updates removed as it was a "dead update" - no fields were being modified.
        }

        // 4. Update Genre-specific Stats (Using optimized lookup)
        foreach (var sg in song.SongGenres)
        {
            var genreName = sg.Genre.Name;
            
            // Optimized: Database lookup instead of loading full list or using Find (IEnumerable)
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
        // Optimized: SQL projection (.Select) to fetch only strings, not full Song entities
        return await _unitOfWork.Repository<ListeningHistory>().Query()
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ListenedAt)
            .Select(h => h.Song.YoutubeVideoId)
            .Distinct()
            .Take(count)
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
