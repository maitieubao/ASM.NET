using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using VibeMusic.Application.Interfaces;

namespace VibeMusic.Controllers;

[Route("api/songs")]
[ApiController]
public class ViewCountController : BaseController
{
    private readonly IViewCountService _viewCountService;
    private readonly ISongService _songService;

    public ViewCountController(IViewCountService viewCountService, ISongService songService)
    {
        _viewCountService = viewCountService;
        _songService = songService;
    }

    /// <summary>
    /// Lấy view count của bài hát theo ID.
    /// </summary>
    [HttpGet("{id}/view-count")]
    [AllowAnonymous]
    public async Task<IActionResult> GetViewCount(int id, CancellationToken ct = default)
    {
        var song = await _songService.GetSongByIdAsync(id, ct);
        if (song == null)
            return NotFound();

        var result = await _viewCountService.GetViewCountAsync(id, ct);

        return SuccessResponse(new
        {
            viewCount = result.ViewCount,
            source = result.Source.ToString().ToLower(),
            lastUpdated = result.LastUpdated,
            isFromCache = result.IsFromCache
        });
    }
}
