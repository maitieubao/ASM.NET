using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize]
public class ArtistController : BaseController
{
    private readonly IArtistService _artistService;

    public ArtistController(IArtistService artistService)
    {
        _artistService = artistService;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 18;
        var (artists, totalCount) = await _artistService.GetPaginatedArtistsAsync(page, pageSize);
        
        ViewBag.TotalCount = totalCount;
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        
        return View(artists);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id, int page = 1)
    {
        // Phân trang bài hát của nghệ sĩ (mặc định 10 bài/trang)
        var artist = await _artistService.GetArtistByIdAsync(id, CurrentUserId, page, 10);
        if (artist == null) return NotFoundResponse("Không tìm thấy nghệ sĩ này");
        
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
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> RefreshBio(int id)
    {
        var newBio = await _artistService.RefreshArtistBioAsync(id);
        
        // Trả về JSON để Frontend cập nhật bằng AJAX, tránh reload trang gây ngắt nhạc
        return SuccessResponse(new { success = true, bio = newBio, message = "Đã cập nhật tiểu sử nghệ sĩ từ nguồn quốc tế." });
    }

    [Authorize(Roles = UserRoles.Admin)]
    public IActionResult Create() => View();

    [HttpPost]
    [Authorize(Roles = UserRoles.Admin)]
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

    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var artist = await _artistService.GetArtistByIdAsync(id);
        if (artist == null) return NotFoundResponse();
        return View(artist);
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.Admin)]
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

    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var artist = await _artistService.GetArtistByIdAsync(id);
        if (artist == null) return NotFoundResponse();
        return View(artist);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = UserRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _artistService.DeleteArtistAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
