using Microsoft.AspNetCore.Mvc;
using VibeMusic.Application.Interfaces;
using VibeMusic.Application.Common;
using VibeMusic.Application.DTOs;
using VibeMusic.Models;
using System.Diagnostics;
using System;
using System.Threading.Tasks;
namespace VibeMusic.Controllers;

public class HomeController : BaseController
{
    private readonly IHomeFacade _homeFacade;
    private readonly IPlaybackFacade _playbackFacade;
    private readonly ILogger<HomeController> _logger;
    private readonly IBackgroundQueue _backgroundQueue;

    public HomeController(IHomeFacade homeFacade, IPlaybackFacade playbackFacade, ILogger<HomeController> logger, IBackgroundQueue backgroundQueue)
    {
        _homeFacade = homeFacade;
        _playbackFacade = playbackFacade;
        _logger = logger;
        _backgroundQueue = backgroundQueue;
    }

    public async Task<IActionResult> Index()
    {
        var model = await _homeFacade.BuildHomeViewModelAsync(CurrentUserId, User.Identity?.Name);
        return View(model);
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetHomeSection(string type, bool refresh = false)
    {
        // Personalized sections (e.g. Recommendations) should skip cache if necessary, 
        // but Facade handles personalization logic. 
        var section = await _homeFacade.GetHomeSectionAsync(type, CurrentUserId, refresh);
        if (section == null) return NoContent();

        return PartialView("_HomeSection", section);
    }

    [HttpGet]
    public async Task<IActionResult> Search(string query)
    {
        if (string.IsNullOrEmpty(query)) return BadRequestResponse("Search query cannot be empty.", "EmptyQuery");
        try
        {
            var results = await _homeFacade.SearchAllAsync(query, CurrentUserId);
            // We return just the list because the search.js expects a direct array
            return Ok(results); 
        }
        catch (Exception)
        {
            return BadRequestResponse("An error occurred during search. Please try again.", "SearchError");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStreamUrl(string? videoUrl, string? title = null, string? artist = null, string? query = null, int? durationMs = null)
    {
        if (string.IsNullOrEmpty(videoUrl) && string.IsNullOrEmpty(query)) 
            return BadRequestResponse("Video URL or Search Query must be provided.", "InvalidParams");

        try
        {
            PlaybackStreamDto result;
            if (!string.IsNullOrEmpty(query))
            {
                result = await _playbackFacade.ResolveAndGetStreamAsync(query, title, artist, CurrentUserId, durationMs);
            }
            else
            {
                result = await _playbackFacade.GetStreamAsync(videoUrl!, title, artist, CurrentUserId, durationMs);
            }
            
            if (!string.IsNullOrEmpty(result.Error))
            {
                return BadRequestResponse(result.Message ?? "Không thể tìm thấy luồng âm thanh.", result.Error);
            }

            return SuccessResponse(new { 
                streamUrl = result.StreamUrl, 
                songId = result.SongId, 
                isLiked = result.IsLiked, 
                showAd = result.ShowAd,
                videoId = result.VideoId ?? videoUrl 
            });
        }
        catch (Exception)
        {
            return BadRequestResponse("An error occurred while retrieving the stream.", "PlaybackError");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStreamWarmup(string videoId)
    {
        if (string.IsNullOrEmpty(videoId)) return BadRequest();
        
        // FIX: Dùng BackgroundQueue để serialize DB writes, tránh concurrent DbContext race condition
        // (ObjectDisposedException: ManualResetEventSlim khi nhiều warmup requests đồng thời)
        await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp, ct) =>
        {
            try
            {
                var facade = sp.GetRequiredService<IPlaybackFacade>();
                await facade.GetStreamAsync(videoId, null, null, null);
            }
            catch { /* Silent fail — warmup is best-effort */ }
        });
        
        return Ok(new { warmed = true });
    }

    [HttpGet]
    public async Task<IActionResult> GetRichMetadata(string videoId, string? lang = null)
    {
        var metadata = await _playbackFacade.GetRichMetadataAsync(videoId, lang);
        return SuccessResponse(new {
            status = metadata.Status,
            lyrics = metadata.Lyrics,
            timedLyrics = metadata.TimedLyrics?.Select(l => new {
                startTime = l.StartTime,
                endTime = l.StartTime + l.Duration,
                duration = l.Duration,
                text = l.Text
            }),
            bio = metadata.Bio,
            availableCaptions = metadata.AvailableCaptions
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetVideoDetails(string videoUrl)
    {
        if (string.IsNullOrEmpty(videoUrl)) return BadRequestResponse("URL cannot be empty.");
        
        try 
        {
            // We use GetStreamAsync which resolves/creates the song in our DB and returns the metadata
            var result = await _playbackFacade.GetStreamAsync(videoUrl, null, null, CurrentUserId);
            
            // Lấy PlayCount thực từ DB nếu có SongId
            long realPlayCount = 0;
            if (result.SongId.HasValue)
            {
                var songCount = await _homeFacade.GetSongPlayCountAsync(result.SongId.Value);
                realPlayCount = songCount;
                _logger.LogInformation("[HOME-CONTROLLER] GetVideoDetails - SongId: {SongId}, RealPlayCount: {Count}", result.SongId, realPlayCount);
            }
            else
            {
                _logger.LogWarning("[HOME-CONTROLLER] GetVideoDetails - SongId not found for {VideoUrl}, defaulting views to 0", videoUrl);
            }

            return SuccessResponse(new {
                songId = result.SongId,
                videoId = result.VideoId,
                title = result.Title,
                authorName = result.Author,
                thumbnailUrl = result.ThumbnailUrl,
                viewCount = realPlayCount,
                genre = result.GenreNames.FirstOrDefault() ?? "Music",
                tags = result.GenreNames.Any() ? result.GenreNames : new List<string> { "Music", "Popular" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video details for {VideoUrl}", videoUrl);
            return ErrorResponse("Internal Server Error");
        }
    }
    
    [HttpGet]
    public async Task<IActionResult> GetSongsByArtist(string name)
    {
        var songs = await _homeFacade.GetSongsByArtistAsync(name);
        return SuccessResponse(songs);
    }
    
    [HttpGet]
    public async Task<IActionResult> Discovery(string tag, int page = 1, bool json = false)
    {
        if (string.IsNullOrEmpty(tag)) tag = "Tất cả";
        
        var songs = await _homeFacade.GetDiscoverySongsAsync(tag, page, 25);
        
        if (json) return Ok(songs);
        
        ViewBag.Tag = tag;
        ViewBag.CurrentPage = page;
        
        return View(songs);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
