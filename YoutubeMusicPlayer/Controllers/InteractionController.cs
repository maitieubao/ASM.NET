using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize]
public class InteractionController : Controller
{
    private readonly IInteractionService _interactionService;

    public InteractionController(IInteractionService interactionService)
    {
        _interactionService = interactionService;
    }

    [HttpPost]
    public async Task<IActionResult> UpdateListeningStats(int songId, double durationSeconds)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdString, out int userId))
        {
            await _interactionService.UpdateListeningStatsAsync(userId, songId, durationSeconds);
            return Ok();
        }
        return Unauthorized();
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLike(int songId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdString, out int userId))
        {
            var isLiked = await _interactionService.ToggleLikeAsync(userId, songId);
            return Json(new { success = true, isLiked = isLiked });
        }
        return Unauthorized();
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLikeByYoutubeId(string youtubeId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdString, out int userId))
        {
            var songService = HttpContext.RequestServices.GetRequiredService<ISongService>();
            var song = await songService.GetOrCreateByYoutubeIdAsync(youtubeId);
            if (song != null)
            {
                var isLiked = await _interactionService.ToggleLikeAsync(userId, song.SongId);
                return Json(new { success = true, isLiked = isLiked, songId = song.SongId });
            }
        }
        return Unauthorized();
    }
}
