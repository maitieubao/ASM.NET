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
public class AdminArtistController : BaseController
{
    private readonly IArtistService _artistService;

    public AdminArtistController(IArtistService artistService)
    {
        _artistService = artistService;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string? searchTerm = null, CancellationToken ct = default)
    {
        var (artists, totalCount) = await _artistService.GetPaginatedArtistsAsync(page, pageSize, searchTerm, ct);
        var model = new AdminArtistListViewModel
        {
            Artists = artists,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            SearchTerm = searchTerm
        };
        return View(model);
    }

    public IActionResult Create() => View(new ArtistDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ArtistDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(dto);

        try
        {
            await _artistService.CreateArtistAsync(dto, ct);
            TempData["Success"] = "Thêm nghệ sĩ mới thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi tạo nghệ sĩ: " + ex.Message;
            return View(dto);
        }
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        var a = await _artistService.GetArtistByIdAsync(id, ct: ct);
        if (a == null) return NotFound();
        return View(a);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ArtistDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(dto);

        try
        {
            await _artistService.UpdateArtistAsync(dto, ct);
            TempData["Success"] = "Cập nhật nghệ sĩ thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi cập nhật nghệ sĩ: " + ex.Message;
            return View(dto);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        try
        {
            await _artistService.DeleteArtistAsync(id, ct);
            TempData["Success"] = "Nghệ sĩ đã được xóa.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi xóa nghệ sĩ: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // --- AJAX search for artist selection ---
    [HttpGet]
    public async Task<IActionResult> Search(string term, CancellationToken ct = default)
    {
        var artists = await _artistService.SearchArtistsAsync(term, 10, ct);
        return Json(artists);
    }

    // --- Optimized Toggles (Direct SQL) ---
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleVerified(int id, CancellationToken ct = default)
    {
        var success = await _artistService.ToggleVerifiedStatusAsync(id, ct);
        if (success) return Json(new { success = true });
        return BadRequest(new { message = "Không tìm thấy nghệ sĩ" });
    }
}
