using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using VibeMusic.Application.Common;
using VibeMusic.Application.Interfaces;

namespace VibeMusic.Controllers;

[Authorize(Roles = UserRoles.Admin)]
[Route("Admin/SongMetadata")]
public class AdminSongMetadataController : BaseController
{
    private readonly ISongService _songService;
    private readonly ISongMetadataEnrichmentService _metadataEnrichmentService;

    public AdminSongMetadataController(
        ISongService songService,
        ISongMetadataEnrichmentService metadataEnrichmentService)
    {
        _songService = songService;
        _metadataEnrichmentService = metadataEnrichmentService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [Route("EnrichSong/{id}")]
    public async Task<IActionResult> EnrichSong(int id)
    {
        var songDto = await _songService.GetSongByIdAsync(id);
        if (songDto == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy bài hát." });
        }

        // Map DTO to entity for enrichment service
        var song = new VibeMusic.Domain.Entities.Song
        {
            SongId = songDto.SongId,
            Title = songDto.Title,
            YoutubeVideoId = songDto.YoutubeVideoId,
            Duration = songDto.Duration,
            IsExplicit = songDto.IsExplicit,
            PlayCount = songDto.PlayCount,
            Isrc = songDto.Isrc
        };

        var success = await _metadataEnrichmentService.EnrichSongMetadataAsync(song);
        
        if (success)
        {
            return SuccessResponse(new { 
                success = true, 
                message = "Đã cập nhật metadata bài hát từ Deezer thành công." 
            });
        }
        else
        {
            return SuccessResponse(new { 
                success = false, 
                message = "Không thể cập nhật metadata. Có thể không tìm thấy bài hát trên Deezer." 
            });
        }
    }

    [HttpPost]
    [Route("EnrichMultiple")]
    public async Task<IActionResult> EnrichMultiple([FromBody] int[] songIds)
    {
        if (songIds == null || songIds.Length == 0)
        {
            return BadRequest("Danh sách bài hát không được để trống.");
        }

        if (songIds.Length > 100)
        {
            return BadRequest("Không thể xử lý quá 100 bài hát cùng lúc.");
        }

        var songDtos = await _songService.GetSongsByIdsAsync(songIds);
        var songs = songDtos.Select(dto => new VibeMusic.Domain.Entities.Song
        {
            SongId = dto.SongId,
            Title = dto.Title,
            YoutubeVideoId = dto.YoutubeVideoId,
            Duration = dto.Duration,
            IsExplicit = dto.IsExplicit,
            PlayCount = dto.PlayCount,
            Isrc = dto.Isrc
        }).ToList();

        var enrichedCount = await _metadataEnrichmentService.EnrichSongsMetadataAsync(songs);
        
        return SuccessResponse(new { 
            success = true, 
            message = $"Đã cập nhật metadata cho {enrichedCount}/{songIds.Length} bài hát.",
            enrichedCount = enrichedCount,
            totalCount = songIds.Length
        });
    }

    [HttpPost]
    [Route("RefreshOutdated")]
    public async Task<IActionResult> RefreshOutdated(int batchSize = 50)
    {
        if (batchSize < 1 || batchSize > 200)
        {
            return BadRequest("Batch size phải từ 1 đến 200.");
        }

        var refreshedCount = await _metadataEnrichmentService.RefreshOutdatedMetadataAsync(batchSize);
        
        return SuccessResponse(new { 
            success = true, 
            message = $"Đã làm mới metadata cho {refreshedCount} bài hát.",
            refreshedCount = refreshedCount,
            batchSize = batchSize
        });
    }

    [HttpPost]
    [Route("EnrichArtistSongs/{artistId}")]
    public async Task<IActionResult> EnrichArtistSongs(int artistId)
    {
        var enrichedCount = await _metadataEnrichmentService.EnrichArtistSongsMetadataAsync(artistId);
        
        return SuccessResponse(new { 
            success = true, 
            message = $"Đã cập nhật metadata cho {enrichedCount} bài hát của nghệ sĩ.",
            enrichedCount = enrichedCount,
            artistId = artistId
        });
    }

    [HttpGet]
    [Route("Stats")]
    public async Task<IActionResult> GetMetadataStats()
    {
        // This would require additional methods in the service to get statistics
        // For now, return a placeholder response
        return SuccessResponse(new { 
            message = "Metadata statistics endpoint - to be implemented",
            timestamp = DateTime.UtcNow
        });
    }
}