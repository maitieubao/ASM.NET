using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Models.Admin;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class AdminPlaylistController : Controller
{
    private readonly IPlaylistService _playlistService;
    private readonly ISongService _songService;

    private int CurrentAdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public AdminPlaylistController(IPlaylistService playlistService, ISongService songService)
    {
        _playlistService = playlistService;
        _songService = songService;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string? searchTerm = null, CancellationToken ct = default)
    {
        var (playlists, totalCount) = await _playlistService.GetPaginatedPlaylistsAsync(page, pageSize, searchTerm, ct);
        var model = new AdminPlaylistListViewModel
        {
            Playlists = playlists,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            SearchTerm = searchTerm
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> SearchSongs(string term, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return Json(new object[] { });
        var (songs, _) = await _songService.GetPaginatedSongsAsync(1, 20, term, ct);
        return Json(songs.Select(s => new { 
            id = s.SongId, 
            text = $"{s.Title} - {s.AuthorName}",
            thumbnail = s.ThumbnailUrl
        }));
    }

    public IActionResult CreateFeatured() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFeatured(PlaylistDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(dto);
        
        try
        {
            await _playlistService.CreateFeaturedPlaylistAsync(dto.Title, dto.FeaturedType, dto.Description, dto.CoverImageUrl, ct);
            TempData["Success"] = "Playlist đã được tạo!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Lỗi khi tạo playlist: " + ex.Message);
            return View(dto);
        }
    }

    public async Task<IActionResult> EditFeatured(int id, CancellationToken ct = default)
    {
        var p = await _playlistService.GetPlaylistByIdAsync(id, isAdmin: true, ct: ct);
        if (p == null) return NotFound();
        
        // Performance improvement: Only load recent 20 songs, use AJAX/Search for more
        var (songs, _) = await _songService.GetPaginatedSongsAsync(1, 20, null, ct);
        ViewBag.RecentSongs = songs;
        return View(p);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditFeatured(PlaylistDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            var (songs, _) = await _songService.GetPaginatedSongsAsync(1, 20, null, ct);
            ViewBag.RecentSongs = songs;
            return View(dto);
        }

        try
        {
            await _playlistService.UpdatePlaylistAsync(dto, CurrentAdminId, isAdmin: true, ct);
            TempData["Success"] = "Cập nhật thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Lỗi khi cập nhật: " + ex.Message);
            var (songs, _) = await _songService.GetPaginatedSongsAsync(1, 20, null, ct);
            ViewBag.RecentSongs = songs;
            return View(dto);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSong(int playlistId, int songId, CancellationToken ct = default)
    {
        try
        {
            await _playlistService.AddSongToPlaylistAsync(playlistId, songId, CurrentAdminId, isAdmin: true, ct);
            TempData["Success"] = "Đã thêm bài hát vào playlist!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(EditFeatured), new { id = playlistId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSong(int playlistId, int songId, CancellationToken ct = default)
    {
        try
        {
            await _playlistService.RemoveSongFromPlaylistAsync(playlistId, songId, CurrentAdminId, isAdmin: true, ct);
            TempData["Success"] = "Đã xóa bài hát khỏi playlist!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(EditFeatured), new { id = playlistId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int page = 1, string? searchTerm = null, CancellationToken ct = default)
    {
        try
        {
            await _playlistService.DeletePlaylistAsync(id, CurrentAdminId, isAdmin: true, ct);
            TempData["Success"] = "Đã xóa playlist!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { page, searchTerm });
    }
}
