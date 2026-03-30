using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize]
public class UserController : Controller
{
    private readonly IUserService _userService;
    private readonly ICommentService _commentService;
    private readonly INotificationService _notificationService;
    private readonly IPlaylistService _playlistService;
    private readonly IInteractionService _interactionService;

    public UserController(IUserService userService, 
                          ICommentService commentService, 
                          INotificationService notificationService,
                          IPlaylistService playlistService,
                          IInteractionService interactionService)
    {
        _userService = userService;
        _commentService = commentService;
        _notificationService = notificationService;
        _playlistService = playlistService;
        _interactionService = interactionService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out int userId) ? userId : 0;
    }

    public async Task<IActionResult> Profile()
    {
        var userId = GetCurrentUserId();
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null) return NotFound();

        ViewBag.ListeningHistory = await _userService.GetUserListeningHistoryAsync(userId);
        ViewBag.Notifications = await _notificationService.GetUserNotificationsAsync(userId);
        ViewBag.Playlists = await _playlistService.GetUserPlaylistsAsync(userId);
        ViewBag.TopGenres = await _interactionService.GetTopPreferredGenresAsync(userId);
        
        return View(user);
    }

    public async Task<IActionResult> Edit()
    {
        var userId = GetCurrentUserId();
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(UserDto model)
    {
        var userId = GetCurrentUserId();
        if (model.UserId != userId) return Forbid();

        if (ModelState.IsValid)
        {
            var success = await _userService.UpdateUserAsync(model);
            if (success)
            {
                return RedirectToAction(nameof(Profile));
            }
            ModelState.AddModelError("", "Something went wrong updating your profile.");
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> MarkNotificationRead(int id)
    {
        await _notificationService.MarkAsReadAsync(id);
        return Ok();
    }

    public async Task<IActionResult> History()
    {
        var userId = GetCurrentUserId();
        var history = await _userService.GetUserListeningHistoryAsync(userId);
        return View(history);
    }
}
