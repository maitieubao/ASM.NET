using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Models.Admin;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class AdminSupportController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly INotificationService _notificationService;
    private readonly ISubscriptionService _subscriptionService;

    public AdminSupportController(IDashboardService dashboardService, INotificationService notificationService, ISubscriptionService subscriptionService)
    {
        _dashboardService = dashboardService;
        _notificationService = notificationService;
        _subscriptionService = subscriptionService;
    }

    // --- REPORTS ---
    public async Task<IActionResult> Reports(CancellationToken ct = default)
    {
        var reports = await _dashboardService.GetAllReportsAsync();
        return View(reports);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveReport(int id, bool takeAction, CancellationToken ct = default)
    {
        await _dashboardService.ResolveReportAsync(id, takeAction);
        return RedirectToAction(nameof(Reports));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissReport(int id, CancellationToken ct = default)
    {
        await _dashboardService.DismissReportAsync(id);
        return RedirectToAction(nameof(Reports));
    }

    // --- NOTIFICATIONS ---
    public async Task<IActionResult> Notifications(CancellationToken ct = default)
    {
        var model = await _notificationService.GetAllNotificationsAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendNotification(string title, string message, int? userId, CancellationToken ct = default)
    {
        if (userId.HasValue)
            await _notificationService.SendUserNotificationAsync(userId.Value, title, message);
        else
            await _notificationService.SendSystemNotificationAsync(title, message);
        
        return RedirectToAction(nameof(Notifications));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNotification(int id, CancellationToken ct = default)
    {
        await _notificationService.DeleteNotificationAsync(id);
        return RedirectToAction(nameof(Notifications));
    }

    // --- SUBSCRIPTION PLANS ---
    public async Task<IActionResult> SubscriptionPlans(CancellationToken ct = default)
    {
        var plans = await _subscriptionService.GetAllPlansAsync();
        return View(plans);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePlan(SubscriptionPlanDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(dto);
        await _subscriptionService.CreatePlanAsync(dto);
        return RedirectToAction(nameof(SubscriptionPlans));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePlan(int id, CancellationToken ct = default)
    {
        await _subscriptionService.DeletePlanAsync(id);
        return RedirectToAction(nameof(SubscriptionPlans));
    }
}
