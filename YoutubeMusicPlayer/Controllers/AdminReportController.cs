using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Models.Admin;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class AdminReportController : Controller
{
    private readonly IDashboardService _dashboardService;

    public AdminReportController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string? status = "Pending", CancellationToken ct = default)
    {
        var (reports, totalCount) = await _dashboardService.GetPaginatedReportsAsync(page, pageSize, status, ct);
        
        var model = new AdminReportListViewModel
        {
            Reports = reports,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Status = status
        };
        
        return View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct = default)
    {
        var report = await _dashboardService.GetReportByIdAsync(id, ct);
        if (report == null) return NotFound();
        return View(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, bool takeAction, string? status = "Pending", CancellationToken ct = default)
    {
        try
        {
            await _dashboardService.ResolveReportAsync(id, takeAction, ct);
            TempData["Success"] = "Báo cáo đã được xử lý thành công.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi xử lý báo cáo: " + ex.Message;
        }
        return RedirectToAction(nameof(Index), new { status });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int id, string? status = "Pending", CancellationToken ct = default)
    {
        try
        {
            await _dashboardService.DismissReportAsync(id, ct);
            TempData["Success"] = "Báo cáo đã được bỏ qua.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi bỏ qua báo cáo: " + ex.Message;
        }
        return RedirectToAction(nameof(Index), new { status });
    }
}
