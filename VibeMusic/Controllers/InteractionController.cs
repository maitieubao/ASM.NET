using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IMemoryCache _cache;

    public InteractionController(IInteractionService interactionService, 
                                 ISongService songService,
                                 IBackgroundQueue backgroundQueue,
                                 IMemoryCache cache)
    {
        _interactionService = interactionService;
        _songService = songService;
        _backgroundQueue = backgroundQueue;
        _cache = cache;
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
        try 
        {
            await _interactionService.IncrementPlayCountAsync(songId);

            // Invalidate per-song play count cache để GetVideoDetails trả về số liệu mới nhất
            _cache.Remove($"song_playcount_{songId}");
            // Invalidate universal play counts cache (dùng trong recommendation scoring)
            _cache.Remove("universal_play_counts");

            System.Console.WriteLine($"[DB-VIEW] SUCCESS: RecordView for Song #{songId}");
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
    public async Task<IActionResult> ResetAllViewCounts(CancellationToken ct = default)
    {
        var context = HttpContext.RequestServices.GetRequiredService<YoutubeMusicPlayer.Infrastructure.Persistence.AppDbContext>();
        await context.Database.ExecuteSqlRawAsync("UPDATE songs SET playcount = 0", ct);

        // Clear relevant caches so UI/API reads zero counts immediately.
        _cache.Remove("universal_play_counts");
        _cache.Remove("AdminDashboardStats");
        if (_cache is MemoryCache concreteCache)
        {
            concreteCache.Compact(1.0);
        }

        System.Console.WriteLine("[DB-CLEAN] SUCCESS: All play counts have been reset to 0.");

        return Ok(new { success = true, message = "Đã đặt lại toàn bộ lượt phát về 0." });
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
