using Microsoft.AspNetCore.Mvc;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Models;
using System.Diagnostics;
using System;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Controllers;

public class HomeController : BaseController
{
    private readonly IHomeFacade _homeFacade;
    private readonly IPlaybackFacade _playbackFacade;

    public HomeController(IHomeFacade homeFacade, IPlaybackFacade playbackFacade)
    {
        _homeFacade = homeFacade;
        _playbackFacade = playbackFacade;
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
        catch (Exception ex)
        {
            return BadRequestResponse("An error occurred during search. Please try again.", "SearchError");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStreamUrl(string? videoUrl, string? title = null, string? artist = null, string? query = null)
    {
        if (string.IsNullOrEmpty(videoUrl) && string.IsNullOrEmpty(query)) 
            return BadRequestResponse("Video URL or Search Query must be provided.", "InvalidParams");

        try
        {
            PlaybackStreamDto result;
            if (!string.IsNullOrEmpty(query))
            {
                result = await _playbackFacade.ResolveAndGetStreamAsync(query, title, artist, CurrentUserId);
            }
            else
            {
                result = await _playbackFacade.GetStreamAsync(videoUrl!, title, artist, CurrentUserId);
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
        catch (Exception ex)
        {
            return BadRequestResponse("An error occurred while retrieving the stream.", "PlaybackError");
        }
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
            
            // Re-fetch more metadata if needed, or use the result
            return SuccessResponse(new {
                songId = result.SongId,
                videoId = result.VideoId,
                title = result.Title,
                authorName = result.Author,
                thumbnailUrl = result.ThumbnailUrl,
                viewCount = 123456, // Placeholder or fetch real views if available in result
                genre = "Music",
                tags = new string[] { "Popular", "Music" }
            });
        }
        catch (Exception ex)
        {
            return BadRequestResponse("Failed to retrieve song details.");
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
