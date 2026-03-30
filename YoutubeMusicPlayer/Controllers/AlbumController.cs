using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

public class AlbumController : Controller
{
    private readonly IAlbumService _albumService;

    public AlbumController(IAlbumService albumService)
    {
        _albumService = albumService;
    }

    public async Task<IActionResult> Details(int id)
    {
        var album = await _albumService.GetAlbumByIdAsync(id);
        if (album == null) return NotFound();
        return View(album);
    }
}
