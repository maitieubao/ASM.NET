using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

public class ArtistController : BaseController
{
    private readonly IArtistService _artistService;

    public ArtistController(IArtistService artistService)
    {
        _artistService = artistService;
    }

    public async Task<IActionResult> Index()
    {
        var artists = await _artistService.GetAllArtistsAsync();
        return View(artists);
    }

    public IActionResult Create()
    {
        return View();
    }

    public async Task<IActionResult> Details(int id, int page = 1)
    {
        var artist = await _artistService.GetArtistByIdAsync(id, CurrentUserId, page, 10);
        if (artist == null) return NotFound();
        return View(artist);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleFollow(int id)
    {
        if (CurrentUserId == null) return Unauthorized();
        
        var isFollowing = await _artistService.ToggleFollowAsync(CurrentUserId.Value, id);
        return SuccessResponse(new { success = true, isFollowing = isFollowing });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ArtistDto artistDto)
    {
        if (ModelState.IsValid)
        {
            await _artistService.CreateArtistAsync(artistDto);
            return RedirectToAction(nameof(Index));
        }
        return View(artistDto);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var artist = await _artistService.GetArtistByIdAsync(id);
        if (artist == null) return NotFound();
        return View(artist);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ArtistDto artistDto)
    {
        if (ModelState.IsValid)
        {
            await _artistService.UpdateArtistAsync(artistDto);
            return RedirectToAction(nameof(Index));
        }
        return View(artistDto);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var artist = await _artistService.GetArtistByIdAsync(id);
        if (artist == null) return NotFound();
        return View(artist);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _artistService.DeleteArtistAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RefreshBio(int id)
    {
        await _artistService.RefreshArtistBioAsync(id);
        return RedirectToAction(nameof(Details), new { id = id });
    }
}
