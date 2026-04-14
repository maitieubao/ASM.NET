using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize]
public class UserController : BaseController
{
    private readonly IUserService _userService;
    private readonly IProfileFacade _profileFacade;
    private readonly INotificationService _notificationService;

    public UserController(IUserService userService, 
                          IProfileFacade profileFacade, 
                          INotificationService notificationService)
    {
        _userService = userService;
        _profileFacade = profileFacade;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Profile()
    {
        if (CurrentUserId == null) return Unauthorized();
        var viewModel = await _profileFacade.BuildUserProfileAsync(CurrentUserId.Value);
        
        if (viewModel == null) return NotFound();
        
        return View(viewModel);
    }

    public async Task<IActionResult> Edit()
    {
        if (CurrentUserId == null) return Unauthorized();
        var user = await _userService.GetUserByIdAsync(CurrentUserId.Value);
        if (user == null) return NotFound();

        // Map UserDto to UpdateUserRequest for View compatibility (Fixes USR-02)
        var model = new UpdateUserRequest
        {
            UserId = user.UserId,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl,
            DateOfBirth = user.DateOfBirth
        };
        
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(UpdateUserRequest model)
    {
        if (CurrentUserId == null) return Unauthorized();
        if (model.UserId != CurrentUserId) return Forbid();

        if (ModelState.IsValid)
        {
            var success = await _userService.UpdateUserAsync(model);
            if (success)
            {
                TempData["Success"] = "Đã cập nhật hồ sơ thành công!";
                return RedirectToAction(nameof(Profile));
            }
            ModelState.AddModelError("", "Something went wrong updating your profile.");
        }
        return View(model);
    }

    [HttpPost]
    [Route("User/MarkNotificationRead/{id}")]
    public async Task<IActionResult> MarkNotificationRead(int id)
    {
        await _notificationService.MarkAsReadAsync(id);
        return SuccessResponse(new { success = true, id = id });
    }

    public async Task<IActionResult> History()
    {
        if (CurrentUserId == null) return Unauthorized();
        var history = await _userService.GetUserListeningHistoryAsync(CurrentUserId.Value);
        return View(history);
    }
}
