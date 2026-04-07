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
        var model = await _homeFacade.BuildHomeViewModelAsync(CurrentUserId);
        return View(model);
    }

    [HttpGet]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "type" })]
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
    public async Task<IActionResult> GetStreamUrl(string videoUrl, string? title = null, string? artist = null)
    {
        if (string.IsNullOrEmpty(videoUrl)) return BadRequestResponse("URL cannot be empty.", "EmptyUrl");

        try
        {
            var result = await _playbackFacade.GetStreamAsync(videoUrl, title, artist, CurrentUserId);
            
            if (!string.IsNullOrEmpty(result.Error))
            {
                return SuccessResponse(new { error = result.Error, message = result.Message });
            }

            return SuccessResponse(new { 
                streamUrl = result.StreamUrl, 
                songId = result.SongId, 
                isLiked = result.IsLiked, 
                showAd = result.ShowAd 
            });
        }
        catch (Exception ex)
        {
            return BadRequestResponse("An error occurred while retrieving the stream.", "PlaybackError");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetRichMetadata(string videoId)
    {
        var metadata = await _playbackFacade.GetRichMetadataAsync(videoId);
        return SuccessResponse(new {
            lyrics = metadata.Lyrics,
            bio = metadata.Bio
        });
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
