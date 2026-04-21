using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Models.Admin;

namespace VibeMusic.Controllers;

[Authorize(Roles = "Admin")]
public class AdminUserController : Controller
{
    private readonly IUserService _userService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IInteractionService _interactionService;
    private readonly ISongService _songService;
    private readonly IPlaylistService _playlistService;

    public AdminUserController(
        IUserService userService,
        ISubscriptionService subscriptionService,
        IInteractionService interactionService,
        ISongService songService,
        IPlaylistService playlistService)
    {
        _userService = userService;
        _subscriptionService = subscriptionService;
        _interactionService = interactionService;
        _songService = songService;
        _playlistService = playlistService;
    }

    private int CurrentAdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string? searchTerm = null, CancellationToken ct = default)
    {
        var (users, totalCount) = await _userService.GetPaginatedUsersAsync(page, pageSize, searchTerm, ct);
        var plans = await _subscriptionService.GetActivePlansAsync(ct);

        var model = new AdminUserListViewModel
        {
            Users = users,
            AvailablePlans = plans.OrderBy(p => p.DurationDays).ToList(),
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

        var listeningHistory = await _userService.GetUserListeningHistoryAsync(id, ct);
        var likedSongIds = (await _interactionService.GetLikedSongIdsAsync(id)).ToList();
        var likedSongs = likedSongIds.Count == 0
            ? new List<SongDto>()
            : (await _songService.GetSongsByIdsAsync(likedSongIds, ct))
                .OrderBy(song => likedSongIds.IndexOf(song.SongId))
                .ToList();
        var playlists = await _playlistService.GetUserPlaylistsAsync(id, ct);

        ViewBag.ListeningHistory = listeningHistory;
        ViewBag.LikedSongs = likedSongs;
        ViewBag.Playlists = playlists;

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserLock(int id, CancellationToken ct = default)
    {
        if (id == CurrentAdminId)
        {
            TempData["Error"] = "You cannot lock your own administrative account.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var success = await _userService.ToggleUserLockAsync(id, ct);
        TempData[success ? "Success" : "Error"] = success
            ? "User lock status updated."
            : "Could not update the user lock status.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantPremium(int id, int planId, int page = 1, string? searchTerm = null, CancellationToken ct = default)
    {
        var success = await _userService.GrantPremiumByPlanAsync(id, planId, ct);

        TempData[success ? "Success" : "Error"] = success
            ? "Premium access granted successfully."
            : "Could not grant premium access. User or plan may be invalid.";

        return RedirectToAction(nameof(Index), new { page, searchTerm });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokePremium(int id, int page = 1, string? searchTerm = null, CancellationToken ct = default)
    {
        var success = await _userService.RevokePremiumAsync(id, ct);

        TempData[success ? "Success" : "Error"] = success
            ? "Premium access revoked successfully."
            : "Could not revoke premium access.";

        return RedirectToAction(nameof(Index), new { page, searchTerm });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        if (id == CurrentAdminId)
        {
            TempData["Error"] = "You cannot delete your own administrative account.";
            return RedirectToAction(nameof(Index));
        }

        var success = await _userService.DeleteUserAsync(id, ct);

        TempData[success ? "Success" : "Error"] = success
            ? "User marked as deleted."
            : "Failed to delete user. User may not exist.";

        return RedirectToAction(nameof(Index));
    }
}
