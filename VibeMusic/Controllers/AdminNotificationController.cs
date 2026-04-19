using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class AdminNotificationController : Controller
{
    private readonly INotificationService _notificationService;

    public AdminNotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var model = await _notificationService.GetAllNotificationsAsync(50, ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(SendNotificationRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            if (request.UserId.HasValue)
                await _notificationService.SendUserNotificationAsync(request.UserId.Value, request.Title, request.Message, ct: ct);
            else
                await _notificationService.SendSystemNotificationAsync(request.Title, request.Message, ct: ct);

            TempData["Success"] = "Gửi thông báo thành công!";
        }
        catch (System.Exception ex)
        {
            TempData["Error"] = "Lỗi khi gửi thông báo: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        await _notificationService.DeleteNotificationAsync(id, ct);
        TempData["Success"] = "Đã xóa thông báo.";
        return RedirectToAction(nameof(Index));
    }
}
