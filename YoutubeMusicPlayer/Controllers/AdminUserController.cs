using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Models.Admin;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class AdminUserController : Controller
{
    private readonly IUserService _userService;

    public AdminUserController(IUserService userService)
    {
        _userService = userService;
    }

    private int CurrentAdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string? searchTerm = null, CancellationToken ct = default)
    {
        var (users, totalCount) = await _userService.GetPaginatedUsersAsync(page, pageSize, searchTerm, ct);
        
        var model = new AdminUserListViewModel
        {
            Users = users,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)System.Math.Ceiling(totalCount / (double)pageSize),
            SearchTerm = searchTerm
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct = default)
    {
        var user = await _userService.GetUserByIdAsync(id, ct);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePremium(int id, int page = 1, string? searchTerm = null, CancellationToken ct = default)
    {
        var success = await _userService.TogglePremiumAsync(id, ct);
        
        if (success)
        {
            TempData["Success"] = "User premium status updated successfully.";
        }
        else
        {
            TempData["Error"] = "User not found or update failed.";
        }

        // Maintain current page and search context
        return RedirectToAction(nameof(Index), new { page, searchTerm });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        // Security check: Prevent admin from deleting themselves
        if (id == CurrentAdminId)
        {
            TempData["Error"] = "You cannot delete your own administrative account.";
            return RedirectToAction(nameof(Index));
        }

        var success = await _userService.DeleteUserAsync(id, ct);
        
        if (success)
        {
            TempData["Success"] = "User marked as deleted.";
        }
        else
        {
            TempData["Error"] = "Failed to delete user. User may not exist.";
        }

        return RedirectToAction(nameof(Index));
    }
}
