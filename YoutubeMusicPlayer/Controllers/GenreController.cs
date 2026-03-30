using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

public class GenreController : Controller
{
    private readonly IGenreService _genreService;

    public GenreController(IGenreService genreService)
    {
        _genreService = genreService;
    }

    public async Task<IActionResult> Details(int id)
    {
        var genre = await _genreService.GetGenreByIdWithSongsAsync(id);
        if (genre == null) return NotFound();
        return View(genre);
    }

    public async Task<IActionResult> Index()
    {
        var genres = await _genreService.GetAllGenresAsync();
        return View(genres);
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
