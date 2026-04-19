using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class GenreController : BaseController
{
    private readonly IGenreService _genreService;
    private readonly ISongService _songService;

    public GenreController(IGenreService genreService, ISongService songService)
    {
        _genreService = genreService;
        _songService = songService;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var genres = await _genreService.GetAllGenresAsync();
        return View(genres);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id, int page = 1)
    {
        var genre = await _genreService.GetGenreByIdWithSongsAsync(id);
        if (genre == null) return NotFound();

        const int pageSize = 20;
        var allSongs = genre.Songs.ToList();
        var totalCount = allSongs.Count;

        genre.Songs = allSongs.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return View(genre);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GenreDto genreDto)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _genreService.CreateGenreAsync(genreDto);
                return RedirectToAction(nameof(Index));
            }
            catch (AppException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View(genreDto);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var genre = await _genreService.GetGenreByIdAsync(id);
        if (genre == null) return NotFound();
        return View(genre);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(GenreDto genreDto)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _genreService.UpdateGenreAsync(genreDto);
                return RedirectToAction(nameof(Index));
            }
            catch (AppException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View(genreDto);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var genre = await _genreService.GetGenreByIdAsync(id);
        if (genre == null) return NotFound();
        return View(genre);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            await _genreService.DeleteGenreAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (AppException ex)
        {
            var genre = await _genreService.GetGenreByIdAsync(id);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(genre);
        }
    }
}
