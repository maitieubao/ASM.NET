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
        var genres = await _genreService.GetAllGenresAsync(ct);
        var categories = await _categoryService.GetAllCategoriesAsync(ct);

        var model = new AdminTaxonomyViewModel
        {
            Genres = genres,
            Categories = categories
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
        
        try
        {
            await _genreService.CreateGenreAsync(dto, ct);
            TempData["Success"] = "Thể loại đã được tạo thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (System.Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(dto);
        }
    }

    public async Task<IActionResult> EditGenre(int id, CancellationToken ct = default)
    {
        var g = await _genreService.GetGenreByIdAsync(id, ct);
        if (g == null) return NotFound();
        return View(g);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditGenre(GenreDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(dto);

        try
        {
            await _genreService.UpdateGenreAsync(dto, ct);
            TempData["Success"] = "Thể loại đã được cập nhật thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (System.Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(dto);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGenre(int id, CancellationToken ct = default)
    {
        try
        {
            await _genreService.DeleteGenreAsync(id, ct);
            TempData["Success"] = "Thể loại đã được xóa thành công!";
        }
        catch (System.Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // --- CATEGORIES ---
    public IActionResult CreateCategory() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(CategoryDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(dto);
        
        try
        {
            await _categoryService.CreateCategoryAsync(dto, ct);
            TempData["Success"] = "Danh mục đã được tạo thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (System.Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(dto);
        }
    }

    public async Task<IActionResult> EditCategory(int id, CancellationToken ct = default)
    {
        var c = await _categoryService.GetCategoryByIdAsync(id, ct);
        if (c == null) return NotFound();
        return View(c);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(CategoryDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(dto);

        try
        {
            await _categoryService.UpdateCategoryAsync(dto, ct);
            TempData["Success"] = "Danh mục đã được cập nhật thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (System.Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(dto);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct = default)
    {
        try
        {
            await _categoryService.DeleteCategoryAsync(id, ct);
            TempData["Success"] = "Danh mục đã được xóa thành công!";
        }
        catch (System.Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
