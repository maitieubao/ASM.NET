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
public class AdminPlaylistController : Controller
{
    private readonly IPlaylistService _playlistService;
    private readonly ISongService _songService;

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

    public IActionResult CreateFeatured() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFeatured(PlaylistDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(dto.Title)) return View(dto);
        await _playlistService.CreateFeaturedPlaylistAsync(dto.Title, dto.FeaturedType, dto.Description, dto.CoverImageUrl, ct);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> EditFeatured(int id, CancellationToken ct = default)
    {
        var p = await _playlistService.GetPlaylistByIdAsync(id, ct: ct);
        if (p == null) return NotFound();
        
        ViewBag.AllSongs = await _songService.GetAllSongsAsync(ct);
        return View(p);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditFeatured(PlaylistDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(dto.Title)) return View(dto);
        await _playlistService.UpdatePlaylistAsync(dto, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSong(int playlistId, int songId, CancellationToken ct = default)
    {
        await _playlistService.AddSongToPlaylistAsync(playlistId, songId, null, ct);
        return RedirectToAction(nameof(EditFeatured), new { id = playlistId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSong(int playlistId, int songId, CancellationToken ct = default)
    {
        await _playlistService.RemoveSongFromPlaylistAsync(playlistId, songId, null, ct);
        return RedirectToAction(nameof(EditFeatured), new { id = playlistId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        await _playlistService.DeletePlaylistAsync(id, null, ct);
        return RedirectToAction(nameof(Index));
    }
}
