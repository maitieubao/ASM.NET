using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

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
    [AllowAnonymous]
    public async Task<IActionResult> UpdateListeningStats(int songId, double durationSeconds)
    {
        if (CurrentUserId == null) return Unauthorized();

        var userId = CurrentUserId.Value;

        await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp, ct) =>
        {
            var scopeInteractionService = sp.GetRequiredService<IInteractionService>();
            await scopeInteractionService.UpdateListeningStatsAsync(userId, songId, durationSeconds);
        });

        return Ok();
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> RecordView(int songId)
    {
        // Execute directly to ensure immediate update as requested by USER
        try 
        {
            await _interactionService.IncrementPlayCountAsync(songId);
            System.Console.WriteLine($"[DB-VIEW] SUCCESS: RecordView for Song #{songId} (Synchronous)");
            return Ok(new { success = true });
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"[DB-VIEW] ERROR: Failed to record view for Song #{songId}. {ex.Message}");
            return StatusCode(500);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetAllViewCounts()
    {
        await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp, ct) =>
        {
            var context = sp.GetRequiredService<YoutubeMusicPlayer.Infrastructure.Persistence.AppDbContext>();
            await context.Database.ExecuteSqlRawAsync("UPDATE songs SET playcount = 0");
            System.Console.WriteLine("[DB-CLEAN] SUCCESS: All play counts have been reset to 0.");
        });

        return Ok(new { success = true, message = "Đang tiến hành đặt lại tất cả lượt phát về 0..." });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ToggleLike(int songId)
    {
        if (CurrentUserId == null) return Unauthorized();

        var isLiked = await _interactionService.ToggleLikeAsync(CurrentUserId.Value, songId);
        return SuccessResponse(new { success = true, isLiked = isLiked });
    }

    [HttpPost]
    [Authorize]
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
