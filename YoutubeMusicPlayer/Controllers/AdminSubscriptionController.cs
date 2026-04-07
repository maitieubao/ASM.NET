using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class AdminSubscriptionController : Controller
{
    private readonly ISubscriptionService _subscriptionService;

    public AdminSubscriptionController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var plans = await _subscriptionService.GetAllPlansAsync(ct);
        return View(plans);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubscriptionPlanDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _subscriptionService.CreatePlanAsync(dto, ct);
            TempData["Success"] = "Gói hội viên đã được tạo thành công!";
        }
        catch (System.Exception ex)
        {
            TempData["Error"] = "Lỗi khi tạo gói: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        try
        {
            // Logic improvement: Check for active subscribers before disabling
            int activeSubscribers = await _subscriptionService.GetActiveSubscriberCountAsync(id, ct);
            
            await _subscriptionService.DeletePlanAsync(id, ct);
            
            if (activeSubscribers > 0)
            {
                TempData["Success"] = $"Gói hội viên đã được ngưng kinh doanh. Lưu ý: Có {activeSubscribers} người dùng đang sử dụng gói này.";
            }
            else
            {
                TempData["Success"] = "Gói hội viên đã được ngưng hoạt động.";
            }
        }
        catch (System.Exception ex)
        {
            TempData["Error"] = "Lỗi khi xóa gói: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
