using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Models.ViewModels;
using YoutubeMusicPlayer.Models;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class SongController : BaseController
{
    private readonly ISongService _songService;
    private readonly IAlbumService _albumService;
    private readonly IGenreService _genreService;
    private readonly IBackgroundQueue _backgroundQueue;

    public SongController(
        ISongService songService, 
        IAlbumService albumService, 
        IGenreService genreService,
        IBackgroundQueue backgroundQueue)
    {
        _songService = songService;
        _albumService = albumService;
        _genreService = genreService;
        _backgroundQueue = backgroundQueue;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(string? searchTerm, int page = 1)
    {
        const int pageSize = 10;
        var (songs, totalCount) = await _songService.GetPaginatedSongsAsync(page, pageSize, searchTerm);
        
        var viewModel = new SongIndexViewModel
        {
            Songs = songs,
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize),
            CurrentPage = page,
            SearchTerm = searchTerm
        };

        return View(viewModel);
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
            // Background processing
            await _backgroundQueue.QueueBackgroundWorkItemAsync(async (serviceProvider) =>
            {
                await _songService.ImportFromYoutubeAsync(videoUrl);
            });

            TempData["SuccessMessage"] = "Yêu cầu nhập bài hát đang được xử lý ngầm. Bài hát sẽ xuất hiện trong giây lát.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Failed to queue import: " + ex.Message);
            return View();
        }
    }

    public async Task<IActionResult> Create()
    {
        var albumsTask = _albumService.GetAllAlbumsAsync();
        var genresTask = _genreService.GetAllGenresAsync();

        await Task.WhenAll(albumsTask, genresTask);

        var viewModel = new SongFormViewModel
        {
            Song = new SongDto(),
            Albums = await albumsTask,
            Genres = await genresTask
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SongFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            await _songService.CreateSongAsync(model.Song);
            return RedirectToAction(nameof(Index));
        }

        var albumsTask = _albumService.GetAllAlbumsAsync();
        var genresTask = _genreService.GetAllGenresAsync();
        await Task.WhenAll(albumsTask, genresTask);

        model.Albums = await albumsTask;
        model.Genres = await genresTask;

        return View(model);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var songTask = _songService.GetSongByIdAsync(id);
        var albumsTask = _albumService.GetAllAlbumsAsync();
        var genresTask = _genreService.GetAllGenresAsync();

        await Task.WhenAll(songTask, albumsTask, genresTask);

        var song = await songTask;
        if (song == null) return NotFound();

        var viewModel = new SongFormViewModel
        {
            Song = song,
            Albums = await albumsTask,
            Genres = await genresTask
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SongFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            await _songService.UpdateSongAsync(model.Song);
            return RedirectToAction(nameof(Index));
        }

        var albumsTask = _albumService.GetAllAlbumsAsync();
        var genresTask = _genreService.GetAllGenresAsync();
        await Task.WhenAll(albumsTask, genresTask);

        model.Albums = await albumsTask;
        model.Genres = await genresTask;

        return View(model);
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
