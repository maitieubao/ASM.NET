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
    private readonly IDeezerService _deezerService;
    private readonly IITunesService _itunesService;

    public AlbumController(IAlbumService albumService, IDeezerService deezerService, IITunesService itunesService)
    {
        _albumService = albumService;
        _deezerService = deezerService;
        _itunesService = itunesService;
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

    [AllowAnonymous]
    public async Task<IActionResult> DetailsEx(string source, string id)
    {
        var model = new ExternalAlbumViewModel { Source = source, ExternalId = id };

        if (source.Equals("Deezer", StringComparison.OrdinalIgnoreCase))
        {
            var albums = await _deezerService.SearchAlbumsAsync(id, 1); // Exact ID search logic depends on service implementation, usually ID lookup is separate
            // Let's assume we find it or use a better lookup
            // Actually, I saw GetAlbumTracksAsync in DeezerService but not GetAlbumDetailsAsync.
            // I'll use SearchAlbumsAsync with the ID if possible, or search for the title.
            // Wait, I should add GetAlbumById to IDeezerService ideally.
            
            var tracks = await _deezerService.GetAlbumTracksAsync(id);
            if (!tracks.Any()) return NotFound("Không tìm thấy thông tin album trên Deezer");

            model.Title = tracks.First().AlbumName;
            model.ArtistName = tracks.First().ArtistName;
            model.CoverImageUrl = tracks.First().AlbumImageUrl;
            model.Tracks = tracks.Select(t => new ExternalTrackViewModel {
                Title = t.TrackName,
                ArtistName = t.ArtistName,
                AlbumName = t.AlbumName,
                DurationMs = t.DurationMs,
                TrackNumber = t.TrackNumber
            }).ToList();
        }
        else if (source.Equals("iTunes", StringComparison.OrdinalIgnoreCase))
        {
            var album = await _itunesService.GetAlbumDetailsAsync(id);
            if (album == null) return NotFound("Không tìm thấy thông tin album trên iTunes");

            var tracks = await _itunesService.GetAlbumTracksAsync(id);
            model.Title = album.CollectionName;
            model.ArtistName = album.ArtistName;
            model.CoverImageUrl = album.ArtworkUrl;
            model.ReleaseDate = album.ReleaseDate;
            model.Tracks = tracks.Select(t => new ExternalTrackViewModel {
                Title = t.TrackName,
                ArtistName = t.ArtistName,
                AlbumName = t.CollectionName,
                DurationMs = t.DurationMs,
                TrackNumber = t.TrackNumber
            }).ToList();
        }

        return View("DetailsEx", model);
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
