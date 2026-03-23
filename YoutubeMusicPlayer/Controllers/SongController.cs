using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

public class SongController : Controller
{
    private readonly ISongService _songService;
    private readonly IAlbumService _albumService;
    private readonly IGenreService _genreService;

    public SongController(ISongService songService, IAlbumService albumService, IGenreService genreService)
    {
        _songService = songService;
        _albumService = albumService;
        _genreService = genreService;
    }

    public async Task<IActionResult> Index()
    {
        var songs = await _songService.GetAllSongsAsync();
        return View(songs);
    }

    public IActionResult Import()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(string videoUrl)
    {
        if (string.IsNullOrEmpty(videoUrl))
        {
            ModelState.AddModelError("", "Please provide a YouTube URL.");
            return View();
        }

        try
        {
            await _songService.ImportFromYoutubeAsync(videoUrl);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Failed to import video details: " + ex.Message);
            return View();
        }
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Albums = await _albumService.GetAllAlbumsAsync();
        ViewBag.Genres = await _genreService.GetAllGenresAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SongDto songDto)
    {
        // Handle Genre IDs from the form (checkboxes/multi-select)
        if (ModelState.IsValid)
        {
            await _songService.CreateSongAsync(songDto);
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Albums = await _albumService.GetAllAlbumsAsync();
        ViewBag.Genres = await _genreService.GetAllGenresAsync();
        return View(songDto);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var song = await _songService.GetSongByIdAsync(id);
        if (song == null) return NotFound();
        ViewBag.Albums = await _albumService.GetAllAlbumsAsync();
        ViewBag.Genres = await _genreService.GetAllGenresAsync();
        return View(song);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SongDto songDto)
    {
        if (ModelState.IsValid)
        {
            await _songService.UpdateSongAsync(songDto);
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Albums = await _albumService.GetAllAlbumsAsync();
        ViewBag.Genres = await _genreService.GetAllGenresAsync();
        return View(songDto);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var song = await _songService.GetSongByIdAsync(id);
        if (song == null) return NotFound();
        return View(song);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _songService.DeleteSongAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
