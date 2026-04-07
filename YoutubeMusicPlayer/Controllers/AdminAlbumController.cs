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
public class AdminAlbumController : BaseController
{
    private readonly IAlbumService _albumService;
    private readonly IArtistService _artistService;

    public AdminAlbumController(IAlbumService albumService, IArtistService artistService)
    {
        _albumService = albumService;
        _artistService = artistService;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string? searchTerm = null, CancellationToken ct = default)
    {
        var (albums, totalCount) = await _albumService.GetPaginatedAlbumsAsync(page, pageSize, searchTerm, ct);
        var model = new AdminAlbumListViewModel
        {
            Albums = albums,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            SearchTerm = searchTerm
        };
        return View(model);
    }

    public IActionResult Create() => View(new AlbumDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AlbumDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(dto);

        try
        {
            await _albumService.CreateAlbumAsync(dto, ct);
            TempData["Success"] = "Thêm album mới thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi tạo album: " + ex.Message;
            return View(dto);
        }
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        var album = await _albumService.GetAlbumByIdAsync(id, ct);
        if (album == null) return NotFound();
        return View(album);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AlbumDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(dto);

        try
        {
            await _albumService.UpdateAlbumAsync(dto, ct);
            TempData["Success"] = "Cập nhật album thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi cập nhật album: " + ex.Message;
            return View(dto);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        try
        {
            await _albumService.DeleteAlbumAsync(id, ct);
            TempData["Success"] = "Album đã được xóa.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi xóa album: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // --- Performance Optimization: AJAX search for artist association ---
    [HttpGet]
    public async Task<IActionResult> SearchArtists(string term, CancellationToken ct = default)
    {
        var artists = await _artistService.SearchArtistsAsync(term, 10, ct);
        return Json(artists);
    }
}
