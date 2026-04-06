using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> TogglePremium(int id, CancellationToken ct = default)
    {
        var user = await _userService.GetUserByIdAsync(id, ct);
        if (user != null)
        {
            user.IsPremium = !user.IsPremium;
            await _userService.UpdateUserAsync(user, ct);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        await _userService.DeleteUserAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }
}
