using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize]
public class PlaylistController : BaseController
{
    private readonly IPlaylistService _playlistService;
    private readonly ISongService _songService;
    private readonly IInteractionService _interactionService;

    public PlaylistController(IPlaylistService playlistService, 
                              ISongService songService, 
                              IInteractionService interactionService)
    {
        _playlistService = playlistService;
        _songService = songService;
        _interactionService = interactionService;
    }

    public async Task<IActionResult> Index()
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Auth");

        var playlists = await _playlistService.GetUserPlaylistsAsync(CurrentUserId.Value);
        return View(playlists);
    }

    [HttpGet]
    public async Task<IActionResult> GetPlaylistsJson()
    {
        if (CurrentUserId == null) return Unauthorized();

        var playlists = await _playlistService.GetUserPlaylistsAsync(CurrentUserId.Value);
        return SuccessResponse(playlists);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, string? description)
    {
        if (CurrentUserId != null && !string.IsNullOrWhiteSpace(title))
        {
            await _playlistService.CreatePlaylistAsync(CurrentUserId.Value, title, description);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int playlistId)
    {
        if (CurrentUserId == null) return Unauthorized();
        try
        {
            await _playlistService.DeletePlaylistAsync(playlistId, CurrentUserId.Value, IsAdmin);
            return SuccessResponse(new { success = true });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequestResponse("An error occurred while deleting the playlist.");
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddSong(int playlistId, int songId)
    {
        if (CurrentUserId == null) return Unauthorized();

        try
        {
            await _playlistService.AddSongToPlaylistAsync(playlistId, songId, CurrentUserId.Value, IsAdmin);
            return SuccessResponse(new { success = true });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddSongByYoutubeId(int playlistId, string youtubeId)
    {
        if (CurrentUserId == null) return Unauthorized();

        try
        {
            var song = await _songService.GetOrCreateByYoutubeIdAsync(youtubeId);

            if (song != null)
            {
                await _playlistService.AddSongToPlaylistAsync(playlistId, song.SongId, CurrentUserId.Value, IsAdmin);
                return SuccessResponse(new { success = true, songTitle = song.Title });
            }
            return BadRequestResponse("Không thể xử lý bài hát từ YouTube.");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequestResponse("Lỗi hệ thống khi thêm bài hát.");
        }
    }

    [HttpPost]
    public async Task<IActionResult> RemoveSong(int playlistId, int songId)
    {
        if (CurrentUserId == null) return Unauthorized();
        try
        {
            await _playlistService.RemoveSongFromPlaylistAsync(playlistId, songId, CurrentUserId.Value, IsAdmin);
            return SuccessResponse(new { success = true });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetPlaylistSongs(int id)
    {
        try
        {
            var playlist = await _playlistService.GetPlaylistByIdAsync(id, CurrentUserId, IsAdmin);
            if (playlist == null) return NotFound();
            return SuccessResponse(playlist.Songs.Select(s => new {
                YoutubeVideoId = s.YoutubeVideoId,
                Title = s.Title,
                AuthorName = "Playlist: " + playlist.Title,
                ThumbnailUrl = s.ThumbnailUrl
            }));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var playlist = await _playlistService.GetPlaylistByIdAsync(id, CurrentUserId, IsAdmin);
            if (playlist == null) return NotFound();
            return View(playlist);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var playlist = await _playlistService.GetPlaylistByIdAsync(id, CurrentUserId, IsAdmin);
            if (playlist == null) return NotFound();
            return View(playlist);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PlaylistDto dto)
    {
        if (ModelState.IsValid)
        {
            try
            {
                if (CurrentUserId == null) return Unauthorized();
                await _playlistService.UpdatePlaylistAsync(dto, CurrentUserId.Value, IsAdmin);
                return RedirectToAction(nameof(Details), new { id = dto.PlaylistId });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }
        return View(dto);
    }

    [HttpGet]
    public async Task<IActionResult> LikedSongs(int page = 1)
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Auth");

        const int pageSize = 20;
        
        var (likedSongIds, totalCount) = await _interactionService.GetLikedSongIdsPaginatedAsync(CurrentUserId.Value, page, pageSize);
        var (songs, _) = await _songService.GetSongsByIdsPaginatedAsync(likedSongIds, 1, pageSize);

        var viewModel = new PlaylistDto
        {
            Title = "Bài hát đã thích",
            Description = "Tất cả bài hát bạn đã yêu thích",
            Songs = songs.ToList()
        };
        
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Reorder(int playlistId, [FromBody] List<int> sortedSongIds)
    {
        if (CurrentUserId == null) return Unauthorized();
        if (sortedSongIds == null || !sortedSongIds.Any()) return BadRequestResponse("Dữ liệu không hợp lệ.");

        try
        {
            await _playlistService.ReorderSongsAsync(playlistId, sortedSongIds, CurrentUserId.Value, IsAdmin);
            return SuccessResponse(new { success = true });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequestResponse("Lỗi khi sắp xếp lại danh sách phát.");
        }
    }
}
