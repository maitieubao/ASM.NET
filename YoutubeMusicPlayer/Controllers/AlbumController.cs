using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize]
public class AlbumController : BaseController
{
    private readonly IAlbumService _albumService;

    public AlbumController(IAlbumService albumService)
    {
        _albumService = albumService;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 12;
        var (albums, totalCount) = await _albumService.GetPaginatedAlbumsAsync(page, pageSize);
        
        ViewBag.CurrentPage = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        
        return View(albums);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var album = await _albumService.GetAlbumByIdAsync(id);
        if (album == null) return NotFoundResponse("Không tìm thấy album này");
        
        return View(album);
    }

    [Authorize(Roles = UserRoles.Admin)]
    public IActionResult Create() => View();

    [HttpPost]
    [Authorize(Roles = UserRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AlbumDto albumDto)
    {
        if (ModelState.IsValid)
        {
            await _albumService.CreateAlbumAsync(albumDto);
            return RedirectToAction(nameof(Index));
        }
        return View(albumDto);
    }

    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var album = await _albumService.GetAlbumByIdAsync(id);
        if (album == null) return NotFoundResponse();
        return View(album);
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AlbumDto albumDto)
    {
        if (ModelState.IsValid)
        {
            await _albumService.UpdateAlbumAsync(albumDto);
            return RedirectToAction(nameof(Index));
        }
        return View(albumDto);
    }

    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var album = await _albumService.GetAlbumByIdAsync(id);
        if (album == null) return NotFoundResponse();
        return View(album);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = UserRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _albumService.DeleteAlbumAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
