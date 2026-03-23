using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize]
public class PlaylistController : Controller
{
    private readonly IPlaylistService _playlistService;
    private readonly ISongService _songService;
    private readonly YoutubeMusicPlayer.Domain.Interfaces.IUnitOfWork _unitOfWork;

    public PlaylistController(IPlaylistService playlistService, ISongService songService, YoutubeMusicPlayer.Domain.Interfaces.IUnitOfWork unitOfWork)
    {
        _playlistService = playlistService;
        _songService = songService;
        _unitOfWork = unitOfWork;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out int userId) ? userId : 0;
    }

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return RedirectToAction("Login", "Auth");

        var playlists = await _playlistService.GetUserPlaylistsAsync(userId);
        return View(playlists);
    }

    [HttpGet]
    public async Task<IActionResult> GetPlaylistsJson()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var playlists = await _playlistService.GetUserPlaylistsAsync(userId);
        return Json(playlists);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, string? description)
    {
        var userId = GetCurrentUserId();
        if (userId != 0 && !string.IsNullOrWhiteSpace(title))
        {
            await _playlistService.CreatePlaylistAsync(userId, title, description);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int playlistId)
    {
        var userId = GetCurrentUserId();
        if (userId != 0)
        {
            await _playlistService.DeletePlaylistAsync(playlistId, userId);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddSong(int playlistId, int songId)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        await _playlistService.AddSongToPlaylistAsync(playlistId, songId, userId);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> AddSongByYoutubeId(int playlistId, string youtubeId)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            await _songService.ImportFromYoutubeAsync($"https://youtube.com/watch?v={youtubeId}");
            var song = await _unitOfWork.Repository<YoutubeMusicPlayer.Domain.Entities.Song>()
                .FirstOrDefaultAsync(s => s.YoutubeVideoId == youtubeId);

            if (song != null)
            {
                await _playlistService.AddSongToPlaylistAsync(playlistId, song.SongId, userId);
                return Ok(new { success = true });
            }
            return BadRequest(new { success = false, message = "Could not find imported song." });
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RemoveSong(int playlistId, int songId)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        await _playlistService.RemoveSongFromPlaylistAsync(playlistId, songId, userId);
        return RedirectToAction(nameof(Index)); // Or wherever
    }
}
