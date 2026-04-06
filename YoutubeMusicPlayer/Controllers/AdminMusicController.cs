using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Models.Admin;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Models.Admin;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class AdminMusicController : BaseController
{
    private readonly ISongService _songService;
    private readonly IArtistService _artistService;
    private readonly IAlbumService _albumService;
    private readonly IGenreService _genreService;

    public AdminMusicController(ISongService songService, IArtistService artistService, IAlbumService albumService, IGenreService genreService)
    {
        _songService = songService;
        _artistService = artistService;
        _albumService = albumService;
        _genreService = genreService;
    }

    // --- SONGS ---
    public async Task<IActionResult> Songs(int page = 1, int pageSize = 10, string? searchTerm = null, CancellationToken ct = default)
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

    public async Task<IActionResult> CreateSong(CancellationToken ct = default)
    {
        ViewBag.Genres = await _genreService.GetAllGenresAsync();
        ViewBag.Albums = await _albumService.GetAllAlbumsAsync(ct);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSong(SongDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(dto.Title)) return View(dto);
        await _songService.CreateSongAsync(dto, ct);
        return RedirectToAction(nameof(Songs));
    }

    public async Task<IActionResult> EditSong(int id, CancellationToken ct = default)
    {
        var s = await _songService.GetSongByIdAsync(id, ct);
        if (s == null) return NotFound();
        ViewBag.Genres = await _genreService.GetAllGenresAsync();
        ViewBag.Albums = await _albumService.GetAllAlbumsAsync(ct);
        return View(s);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSong(SongDto dto, CancellationToken ct = default)
    {
        await _songService.UpdateSongAsync(dto, ct);
        return RedirectToAction(nameof(Songs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSong(int id, CancellationToken ct = default)
    {
        await _songService.DeleteSongAsync(id, ct);
        return RedirectToAction(nameof(Songs));
    }

    // --- ARTISTS ---
    public async Task<IActionResult> Artists(int page = 1, int pageSize = 10, string? searchTerm = null, CancellationToken ct = default)
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

    public IActionResult CreateArtist() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateArtist(ArtistDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(dto.Name)) return View(dto);
        await _artistService.CreateArtistAsync(dto, ct);
        return RedirectToAction(nameof(Artists));
    }

    public async Task<IActionResult> EditArtist(int id, CancellationToken ct = default)
    {
        var a = await _artistService.GetArtistByIdAsync(id, ct: ct);
        if (a == null) return NotFound();
        return View(a);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditArtist(ArtistDto dto, CancellationToken ct = default)
    {
        await _artistService.UpdateArtistAsync(dto, ct);
        return RedirectToAction(nameof(Artists));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteArtist(int id, CancellationToken ct = default)
    {
        await _artistService.DeleteArtistAsync(id, ct);
        return RedirectToAction(nameof(Artists));
    }

    // --- ALBUMS ---
    public async Task<IActionResult> Albums(int page = 1, int pageSize = 10, string? searchTerm = null, CancellationToken ct = default)
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

    public async Task<IActionResult> CreateAlbum(CancellationToken ct = default)
    {
        ViewBag.Artists = await _artistService.GetAllArtistsAsync(ct);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAlbum(AlbumDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(dto.Title)) return View(dto);
        await _albumService.CreateAlbumAsync(dto, ct);
        return RedirectToAction(nameof(Albums));
    }

    public async Task<IActionResult> EditAlbum(int id, CancellationToken ct = default)
    {
        var al = await _albumService.GetAlbumByIdAsync(id, ct);
        if (al == null) return NotFound();
        ViewBag.Artists = await _artistService.GetAllArtistsAsync(ct);
        return View(al);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAlbum(AlbumDto dto, CancellationToken ct = default)
    {
        await _albumService.UpdateAlbumAsync(dto, ct);
        return RedirectToAction(nameof(Albums));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleSongPremium(int id, CancellationToken ct = default)
    {
        var song = await _songService.GetSongByIdAsync(id, ct);
        if (song != null)
        {
            song.IsPremiumOnly = !song.IsPremiumOnly;
            await _songService.UpdateSongAsync(song, ct);
            return SuccessResponse(new { success = true });
        }
        return ErrorResponse("Song not found");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleSongExplicit(int id, CancellationToken ct = default)
    {
        var song = await _songService.GetSongByIdAsync(id, ct);
        if (song != null)
        {
            song.IsExplicit = !song.IsExplicit;
            await _songService.UpdateSongAsync(song, ct);
            return SuccessResponse(new { success = true });
        }
        return ErrorResponse("Song not found");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleArtistVerified(int id, CancellationToken ct = default)
    {
        var artist = await _artistService.GetArtistByIdAsync(id, ct: ct);
        if (artist != null)
        {
            artist.IsVerified = !artist.IsVerified;
            await _artistService.UpdateArtistAsync(artist, ct);
            return SuccessResponse(new { success = true });
        }
        return ErrorResponse("Artist not found");
    }
}
