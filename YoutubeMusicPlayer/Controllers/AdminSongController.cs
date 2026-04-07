using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Models.Admin;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class AdminSongController : BaseController
{
    private readonly ISongService _songService;
    private readonly IAlbumService _albumService;
    private readonly IGenreService _genreService;

    public AdminSongController(ISongService songService, IAlbumService albumService, IGenreService genreService)
    {
        _songService = songService;
        _albumService = albumService;
        _genreService = genreService;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string? searchTerm = null, CancellationToken ct = default)
    {
        var (songs, totalCount) = await _songService.GetPaginatedSongsAsync(page, pageSize, searchTerm, ct);
        var model = new AdminSongListViewModel
        {
            Songs = songs,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            SearchTerm = searchTerm
        };
        return View(model);
    }

    public async Task<IActionResult> Create(CancellationToken ct = default)
    {
        ViewBag.Genres = await _genreService.GetAllGenresAsync();
        return View(new SongDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SongDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Genres = await _genreService.GetAllGenresAsync();
            return View(dto);
        }

        try
        {
            await _songService.CreateSongAsync(dto, ct);
            TempData["Success"] = "Thêm bài hát mới thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi tạo bài hát: " + ex.Message;
            ViewBag.Genres = await _genreService.GetAllGenresAsync();
            return View(dto);
        }
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        var song = await _songService.GetSongByIdAsync(id, ct);
        if (song == null) return NotFound();

        ViewBag.Genres = await _genreService.GetAllGenresAsync();
        // The View will use SearchAlbums API to load the album name for the dropdown
        return View(song);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SongDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Genres = await _genreService.GetAllGenresAsync();
            return View(dto);
        }

        try
        {
            await _songService.UpdateSongAsync(dto, ct);
            TempData["Success"] = "Cập nhật bài hát thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi cập nhật bài hát: " + ex.Message;
            ViewBag.Genres = await _genreService.GetAllGenresAsync();
            return View(dto);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        try
        {
            await _songService.DeleteSongAsync(id, ct);
            TempData["Success"] = "Bài hát đã được xóa.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi xóa bài hát: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // --- Performance Optimization: AJAX search for dropdowns ---
    [HttpGet]
    public async Task<IActionResult> SearchAlbums(string term, CancellationToken ct = default)
    {
        var albums = await _albumService.SearchAlbumsAsync(term, 10, ct);
        return Json(albums);
    }

    // --- Optimized Toggles (Direct SQL) ---
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePremium(int id, CancellationToken ct = default)
    {
        var success = await _songService.TogglePremiumStatusAsync(id, ct);
        if (success) return Json(new { success = true });
        return BadRequest(new { message = "Không tìm thấy bài hát" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleExplicit(int id, CancellationToken ct = default)
    {
        var success = await _songService.ToggleExplicitStatusAsync(id, ct);
        if (success) return Json(new { success = true });
        return BadRequest(new { message = "Không tìm thấy bài hát" });
    }
}
