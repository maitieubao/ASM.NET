using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Models.Admin;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class AdminReportController : Controller
{
    private readonly IDashboardService _dashboardService;

    public AdminReportController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 10,
        string? status = "Pending",
        string? searchTerm = null,
        CancellationToken ct = default)
    {
        if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            status = null;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var filtered = (await _dashboardService.GetAllReportsAsync(ct))
                .Where(report =>
                    (string.IsNullOrWhiteSpace(status) || string.Equals(report.Status, status, StringComparison.OrdinalIgnoreCase)) &&
                    (report.UserName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                     report.TargetName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                     report.TargetType.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                     report.Reason.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                     (!string.IsNullOrWhiteSpace(report.Details) && report.Details.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            var paged = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(new AdminReportListViewModel
            {
                Reports = paged,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(filtered.Count / (double)pageSize),
                Status = status,
                SearchTerm = searchTerm
            });
        }

        var (reports, totalCount) = await _dashboardService.GetPaginatedReportsAsync(page, pageSize, status, ct);

        return View(new AdminReportListViewModel
        {
            Reports = reports,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Status = status,
            SearchTerm = searchTerm
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct = default)
    {
        var report = await _dashboardService.GetReportByIdAsync(id, ct);
        if (report == null) return NotFound();
        return View(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, bool takeAction, string? status = "Pending", string? searchTerm = null, CancellationToken ct = default)
    {
        try
        {
            await _dashboardService.ResolveReportAsync(id, takeAction, ct);
            TempData["Success"] = "Report processed successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Failed to process the report: " + ex.Message;
        }

        return RedirectToAction(nameof(Index), new { status, searchTerm });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int id, string? status = "Pending", string? searchTerm = null, CancellationToken ct = default)
    {
        try
        {
            await _dashboardService.DismissReportAsync(id, ct);
            TempData["Success"] = "Report dismissed successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Failed to dismiss the report: " + ex.Message;
        }

        return RedirectToAction(nameof(Index), new { status, searchTerm });
    }
}
