using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

public class GenreController : BaseController
{
    private readonly IGenreService _genreService;
    private readonly ISongService _songService;

    public GenreController(IGenreService genreService, ISongService songService)
    {
        _genreService = genreService;
        _songService = songService;
    }

    public async Task<IActionResult> Index()
    {
        var genres = await _genreService.GetAllGenresAsync();
        return View(genres);
    }

    public async Task<IActionResult> Details(int id, int page = 1)
    {
        var genre = await _genreService.GetGenreByIdAsync(id);
        if (genre == null) return NotFound();

        const int pageSize = 20;
        var (songs, totalCount) = await _genreService.GetSongsByGenrePaginatedAsync(id, page, pageSize);

        ViewBag.Songs = songs;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

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
            await _genreService.CreateGenreAsync(genreDto);
            return RedirectToAction(nameof(Index));
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
            await _genreService.UpdateGenreAsync(genreDto);
            return RedirectToAction(nameof(Index));
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
        await _genreService.DeleteGenreAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
