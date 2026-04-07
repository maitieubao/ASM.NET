using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize]
public class InteractionController : BaseController
{
    private readonly IInteractionService _interactionService;
    private readonly ISongService _songService;
    private readonly IBackgroundQueue _backgroundQueue;

    public InteractionController(IInteractionService interactionService, 
                                 ISongService songService,
                                 IBackgroundQueue backgroundQueue)
    {
        _interactionService = interactionService;
        _songService = songService;
        _backgroundQueue = backgroundQueue;
    }

    [HttpPost]
    public async Task<IActionResult> UpdateListeningStats(int songId, double durationSeconds)
    {
        if (CurrentUserId == null) return Unauthorized();

        var userId = CurrentUserId.Value;

        await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp) =>
        {
            var scopeInteractionService = sp.GetRequiredService<IInteractionService>();
            await scopeInteractionService.UpdateListeningStatsAsync(userId, songId, durationSeconds);
        });

        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLike(int songId)
    {
        if (CurrentUserId == null) return Unauthorized();

        var isLiked = await _interactionService.ToggleLikeAsync(CurrentUserId.Value, songId);
        return SuccessResponse(new { success = true, isLiked = isLiked });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLikeByYoutubeId(string youtubeId)
    {
        if (CurrentUserId == null) return Unauthorized();

        var song = await _songService.GetOrCreateByYoutubeIdAsync(youtubeId);
        if (song != null)
        {
            var isLiked = await _interactionService.ToggleLikeAsync(CurrentUserId.Value, song.SongId);
            return SuccessResponse(new { success = true, isLiked = isLiked, songId = song.SongId });
        }

        return BadRequestResponse("Không thể xử lý bài hát từ YouTube ID này.");
    }
}
