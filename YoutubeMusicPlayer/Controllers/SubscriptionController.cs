using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

public class SubscriptionController : BaseController
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IYoutubeService _youtubeService;

    public SubscriptionController(ISubscriptionService subscriptionService, IYoutubeService youtubeService)
    {
        _subscriptionService = subscriptionService;
        _youtubeService = youtubeService;
    }

    public async Task<IActionResult> Index()
    {
        // 1. Get plans sequentially to avoid DbContext concurrency
        var plans = await _subscriptionService.GetActivePlansAsync();
        
        // 2. Check for user status sequentially
        ViewBag.IsPremium = false;
        if (CurrentUserId.HasValue)
        {
            ViewBag.IsPremium = await _subscriptionService.IsUserPremiumAsync(CurrentUserId.Value);
        }

        return View(plans);
    }

    [Authorize]
    public async Task<IActionResult> Transactions()
    {
        var userId = CurrentUserId;
        if (!userId.HasValue) return Unauthorized();

        var payments = await _subscriptionService.GetUserPaymentsAsync(userId.Value);
        return View(payments);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CancelSubscription()
    {
        var userId = CurrentUserId;
        if (!userId.HasValue) return Unauthorized();

        var success = await _subscriptionService.CancelSubscriptionAsync(userId.Value);
        if (success) TempData["Message"] = "Đã hủy gói thành công. Bạn sẽ không còn quyền lợi Premium.";
        else TempData["Error"] = "Không tìm thấy gói Premium đang hoạt động để hủy.";
        
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    public async Task<IActionResult> Download(string youtubeId, string title)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue) return Unauthorized();

        // Premium validation
        if (!(await _subscriptionService.IsUserPremiumAsync(userId.Value)))
        {
            TempData["Error"] = "Chỉ hội viên Premium mới có thể tải nhạc. Hãy nâng cấp gói của bạn!";
            return Redirect(Request.Headers["Referer"].ToString() ?? "/Subscription");
        }

        var videoUrl = $"https://youtube.com/watch?v={youtubeId}";
        var streamUrl = await _youtubeService.GetAudioStreamUrlAsync(videoUrl);

        if (string.IsNullOrEmpty(streamUrl)) return BadRequest("Không thể lấy dữ liệu để tải về.");

        return Redirect(streamUrl); 
    }
}
