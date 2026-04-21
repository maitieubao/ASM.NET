using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Interfaces;

namespace VibeMusic.Controllers;

[Route("Admin")]
[Route("AdminDashboard")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly IMemoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private const string StatsCacheKey = "AdminDashboardStats";

    public AdminDashboardController(IDashboardService dashboardService, IMemoryCache cache, IUnitOfWork unitOfWork)
    {
        _dashboardService = dashboardService;
        _cache = cache;
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        // Try to get cached data first to protect DB performance
        DashboardDto? stats = null;
        if (!_cache.TryGetValue(StatsCacheKey, out stats))
        {
            try
            {
                stats = await _dashboardService.GetStatsAsync(ct);

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));

                _cache.Set(StatsCacheKey, stats, cacheOptions);
            }
            catch (Exception ex)
            {
                // Fallback: Show empty dashboard instead of crashing with 500 error
                TempData["Error"] = "Lỗi khi tải số liệu thống kê: " + ex.Message;
                return View(new DashboardDto());
            }
        }

        return View(stats ?? new DashboardDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RefreshStats()
    {
        _cache.Remove(StatsCacheKey);
        TempData["Success"] = "Đã làm mới số liệu thống kê thành công.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Reset PlayCount ảo (từ YouTube ViewCount/10000 khi import) về số lượt nghe thực từ ListeningHistory.
    /// Chỉ admin mới có quyền thực hiện.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncPlayCountFromHistory(CancellationToken ct = default)
    {
        try
        {
            // Đếm lượt nghe thực từ ListeningHistory theo từng SongId
            var realCounts = await _unitOfWork.Repository<VibeMusic.Domain.Entities.ListeningHistory>()
                .Query()
                .AsNoTracking()
                .GroupBy(h => h.SongId)
                .Select(g => new { SongId = g.Key, Count = (long)g.Count() })
                .ToListAsync(ct);

            var countDict = realCounts.ToDictionary(x => x.SongId, x => x.Count);

            // Lấy tất cả bài hát
            var songs = await _unitOfWork.Repository<VibeMusic.Domain.Entities.Song>()
                .Query()
                .Where(s => !s.IsDeleted)
                .ToListAsync(ct);

            int updated = 0;
            foreach (var song in songs)
            {
                var realCount = countDict.TryGetValue(song.SongId, out var c) ? c : 0;
                if (song.PlayCount != realCount)
                {
                    song.PlayCount = realCount;
                    _unitOfWork.Repository<VibeMusic.Domain.Entities.Song>().Update(song);
                    updated++;
                }
            }

            await _unitOfWork.CompleteAsync(ct);

            // Invalidate dashboard cache
            _cache.Remove(StatsCacheKey);

            TempData["Success"] = $"Đã đồng bộ lượt phát thực tế cho {updated} bài hát từ lịch sử nghe nhạc.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi đồng bộ: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAllPlayCounts(CancellationToken ct = default)
    {
        try
        {
            int affected = await _unitOfWork.ExecuteSqlRawAsync(
                "UPDATE songs SET playcount = 0 WHERE is_deleted = false",
                ct);

            _cache.Remove(StatsCacheKey);
            _cache.Remove("universal_play_counts");

            TempData["Success"] = $"Đã reset lượt phát về 0 cho {affected} bài hát.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi reset lượt phát: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
