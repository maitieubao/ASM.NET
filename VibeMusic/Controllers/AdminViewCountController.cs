using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;

namespace VibeMusic.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/admin/songs")]
[ApiController]
public class AdminViewCountController : BaseController
{
    private readonly IViewCountService _viewCountService;
    private readonly ISongService _songService;
    private readonly IBackgroundQueue _backgroundQueue;

    public AdminViewCountController(
        IViewCountService viewCountService,
        ISongService songService,
        IBackgroundQueue backgroundQueue)
    {
        _viewCountService = viewCountService;
        _songService = songService;
        _backgroundQueue = backgroundQueue;
    }

    /// <summary>
    /// Enqueue sync job để đồng bộ view count từ các nền tảng bên ngoài.
    /// </summary>
    [HttpPost("{id}/sync-view-count")]
    public async Task<IActionResult> SyncViewCount(int id, CancellationToken ct = default)
    {
        var song = await _songService.GetSongByIdAsync(id, ct);
        if (song == null)
            return NotFound();

        await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp, cancellationToken) =>
        {
            var syncService = sp.GetRequiredService<IExternalViewCountSyncService>();
            await syncService.SyncAllSourcesAsync(id, cancellationToken);
        });

        return Accepted(new { message = "Sync job enqueued", songId = id });
    }

    /// <summary>
    /// Cập nhật nguồn view count ưu tiên cho bài hát.
    /// </summary>
    [HttpPost("{id}/priority-source")]
    public async Task<IActionResult> SetPrioritySource(int id, [FromBody] SetPrioritySourceRequest request, CancellationToken ct = default)
    {
        var song = await _songService.GetSongByIdAsync(id, ct);
        if (song == null)
            return NotFound();

        await _viewCountService.UpdatePrioritySourceAsync(id, request.Source, ct);

        return SuccessResponse(new { success = true });
    }
}

/// <summary>
/// Request body cho endpoint set priority source.
/// </summary>
public class SetPrioritySourceRequest
{
    public ViewCountSource? Source { get; set; }
}
