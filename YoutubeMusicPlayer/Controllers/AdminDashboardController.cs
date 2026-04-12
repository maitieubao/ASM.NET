using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Route("Admin")]
[Route("AdminDashboard")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly IMemoryCache _cache;
    private const string StatsCacheKey = "AdminDashboardStats";

    public AdminDashboardController(IDashboardService dashboardService, IMemoryCache cache)
    {
        _dashboardService = dashboardService;
        _cache = cache;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        // Try to get cached data first to protect DB performance
        if (!_cache.TryGetValue(StatsCacheKey, out DashboardDto stats))
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

        return View(stats);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RefreshStats()
    {
        _cache.Remove(StatsCacheKey);
        TempData["Success"] = "Đã làm mới số liệu thống kê thành công.";
        return RedirectToAction(nameof(Index));
    }
}
