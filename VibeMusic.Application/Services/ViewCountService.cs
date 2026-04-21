using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;

namespace VibeMusic.Application.Services;

public class ViewCountService : IViewCountService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ViewCountService> _logger;

    private static string CacheKey(int songId) => $"viewcount_{songId}";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public ViewCountService(IUnitOfWork unitOfWork, IMemoryCache cache, ILogger<ViewCountService> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ViewCountResult> GetViewCountAsync(int songId, CancellationToken ct = default)
    {
        // 1. Cache check
        if (_cache.TryGetValue(CacheKey(songId), out ViewCountResult? cached) && cached != null)
            return new ViewCountResult
            {
                ViewCount = cached.ViewCount,
                Source = cached.Source,
                LastUpdated = cached.LastUpdated,
                IsFromCache = true
            };

        try
        {
            // 2. Load song + external view counts
            var song = await _unitOfWork.Repository<Song>().Query()
                .AsNoTracking()
                .Include(s => s.ExternalViewCounts.Where(e => e.IsActive))
                .FirstOrDefaultAsync(s => s.SongId == songId && !s.IsDeleted, ct);

            if (song == null)
                return new ViewCountResult
                {
                    ViewCount = 0,
                    Source = ViewCountSource.Internal,
                    LastUpdated = DateTime.UtcNow
                };

            ViewCountResult result;

            if (song.PrioritySource.HasValue)
            {
                // 3. Use specified priority source
                var ext = song.ExternalViewCounts.FirstOrDefault(e => e.Source == song.PrioritySource.Value);
                result = ext != null
                    ? new ViewCountResult { ViewCount = ext.ViewCount, Source = ext.Source, LastUpdated = ext.LastUpdated }
                    : new ViewCountResult { ViewCount = song.PlayCount, Source = ViewCountSource.Internal, LastUpdated = DateTime.UtcNow };
            }
            else if (song.ExternalViewCounts.Any())
            {
                // 4. Auto: pick highest across ALL sources (external + internal)
                var bestExternal = song.ExternalViewCounts.OrderByDescending(e => e.ViewCount).First();
                if (song.PlayCount >= bestExternal.ViewCount)
                {
                    // Internal is highest
                    result = new ViewCountResult { ViewCount = song.PlayCount, Source = ViewCountSource.Internal, LastUpdated = DateTime.UtcNow };
                }
                else
                {
                    result = new ViewCountResult { ViewCount = bestExternal.ViewCount, Source = bestExternal.Source, LastUpdated = bestExternal.LastUpdated };
                }
            }
            else
            {
                // 5. Fallback to internal
                result = new ViewCountResult { ViewCount = song.PlayCount, Source = ViewCountSource.Internal, LastUpdated = DateTime.UtcNow };
            }

            _cache.Set(CacheKey(songId), result, CacheTtl);
            return result;
        }
        catch (Exception ex)
        {
            // 6. Exception fallback
            _logger.LogWarning(ex, "ViewCountService fallback to internal for song {SongId}", songId);
            var song = await _unitOfWork.Repository<Song>().Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SongId == songId, ct);
            return new ViewCountResult
            {
                ViewCount = song?.PlayCount ?? 0,
                Source = ViewCountSource.Internal,
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    public async Task UpdatePrioritySourceAsync(int songId, ViewCountSource? source, CancellationToken ct = default)
    {
        var song = await _unitOfWork.Repository<Song>().GetByIdAsync(songId, ct);
        if (song == null) return;

        song.PrioritySource = source;
        _unitOfWork.Repository<Song>().Update(song);
        await _unitOfWork.CompleteAsync(ct);
        _cache.Remove(CacheKey(songId));
    }
}
