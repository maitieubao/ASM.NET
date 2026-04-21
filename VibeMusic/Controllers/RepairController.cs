using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;
using System.Threading.Tasks;

namespace VibeMusic.Controllers;

public class RepairController : BaseController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISongService _songService;

    public RepairController(IUnitOfWork unitOfWork, ISongService songService)
    {
        _unitOfWork = unitOfWork;
        _songService = songService;
    }

    [HttpGet]
    public async Task<IActionResult> FixSong()
    {
        var youtubeId = "1So7VBehCQg"; // Đừng làm trái tim anh đau
        
        var song = await _unitOfWork.Repository<Song>().Query()
            .FirstOrDefaultAsync(s => s.YoutubeVideoId == youtubeId && !s.IsDeleted);

        if (song == null)
        {
            return Content("Song not found in database.");
        }

        // 1. Reset PlayCount
        song.PlayCount = 1;
        
        // 2. Trigger enrichment to fix artist if it's "Nghệ sĩ"
        _unitOfWork.Repository<Song>().Update(song);
        await _unitOfWork.CompleteAsync();

        // 3. Force background enrichment
        await _songService.EnrichSongAsync(song.SongId);

        return Content($"SUCCESS: Song '{song.Title}' has been reset. PlayCount: {song.PlayCount}. Enrichment triggered.");
    }
}
