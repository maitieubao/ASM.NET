using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Models.Admin;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class AdminTaxonomyController : Controller
{
    private readonly IGenreService _genreService;
    private readonly ICategoryService _categoryService;

    public AdminTaxonomyController(IGenreService genreService, ICategoryService categoryService)
    {
        _genreService = genreService;
        _categoryService = categoryService;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var model = new AdminTaxonomyViewModel
        {
            Genres = await _genreService.GetAllGenresAsync(),
            Categories = await _categoryService.GetAllCategoriesAsync()
        };
        return View(model);
    }

    // --- GENRES ---
    public IActionResult CreateGenre() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGenre(GenreDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(dto);
        await _genreService.CreateGenreAsync(dto); // Missing ct in simple services, but adding for future
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> EditGenre(int id, CancellationToken ct = default)
    {
        var g = await _genreService.GetGenreByIdAsync(id);
        if (g == null) return NotFound();
        return View(g);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditGenre(GenreDto dto, CancellationToken ct = default)
    {
        await _genreService.UpdateGenreAsync(dto);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGenre(int id, CancellationToken ct = default)
    {
        await _genreService.DeleteGenreAsync(id);
        return RedirectToAction(nameof(Index));
    }

    // --- CATEGORIES ---
    public IActionResult CreateCategory() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(CategoryDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(dto);
        await _categoryService.CreateCategoryAsync(dto);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> EditCategory(int id, CancellationToken ct = default)
    {
        var c = await _categoryService.GetCategoryByIdAsync(id);
        if (c == null) return NotFound();
        return View(c);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(CategoryDto dto, CancellationToken ct = default)
    {
        await _categoryService.UpdateCategoryAsync(dto);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct = default)
    {
        await _categoryService.DeleteCategoryAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
