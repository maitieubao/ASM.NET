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
    private readonly IHttpClientFactory _httpClientFactory;

    public SubscriptionController(ISubscriptionService subscriptionService, IYoutubeService youtubeService, IHttpClientFactory httpClientFactory)
    {
        _subscriptionService = subscriptionService;
        _youtubeService = youtubeService;
        _httpClientFactory = httpClientFactory;
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

        // 1. Detailed Premium Validation with Logging
        var now = DateTime.UtcNow;
        var sub = await _subscriptionService.IsUserPremiumAsync(userId.Value);
        
        Console.WriteLine($"[DOWNLOAD-AUTH] User: {userId}, IsPremium: {sub}, ServerTime_UTC: {now:yyyy-MM-dd HH:mm:ss}");

        if (!sub)
        {
            Console.WriteLine($"[DOWNLOAD-DENIED] User {userId} still failed premium check.");
            // Return 403 Forbidden - Browsers won't try to download this as a text file
            return Forbid(); 
        }

        try 
        {
            Console.WriteLine($"[DOWNLOAD] Fetching audio as MP4 for ID: {youtubeId}...");
            var videoUrl = $"https://youtube.com/watch?v={youtubeId}";
            var streamUrl = await _youtubeService.GetAudioStreamUrlAsync(videoUrl);

            if (string.IsNullOrEmpty(streamUrl)) 
            {
                Console.WriteLine("[DOWNLOAD-ERROR] Stream URL empty.");
                return NotFound("Không tìm thấy luồng tải về.");
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(20); 
            
            // Set Request Headers to mimic a browser/player for better YouTube compatibility
            var request = new HttpRequestMessage(HttpMethod.Get, streamUrl);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var response = await client.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
            
            if (!response.IsSuccessStatusCode) 
            {
                Console.WriteLine($"[DOWNLOAD-ERROR] YouTube source error: {response.StatusCode}");
                return StatusCode((int)response.StatusCode, "Lỗi từ máy chủ nguồn.");
            }

            var stream = await response.Content.ReadAsStreamAsync();
            
            // Final filename logic - strictly .mp4
            string safeTitle = string.Join("_", (title ?? "Music-Track").Split(System.IO.Path.GetInvalidFileNameChars()));
            if (!safeTitle.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) {
                safeTitle += ".mp4";
            }
            
            Console.WriteLine($"[DOWNLOAD-SUCCESS] Sending file: {safeTitle}");

            // CRITICAL: Overwrite headers to force audio/mp4 and attachment
            Response.Headers.Clear(); // Clear any previous headers (like content-type: text/plain)
            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{safeTitle}\"");
            Response.Headers.Append("Content-Type", "audio/mp4");
            Response.Headers.Append("X-Content-Type-Options", "nosniff");

            return File(stream, "audio/mp4", safeTitle);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DOWNLOAD-FATAL] Exception: {ex.Message}");
            return StatusCode(500, "Lỗi hệ thống trong quá trình xử lý âm thanh.");
        }
    }
}
