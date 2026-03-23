using System;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YoutubeMusicPlayer.Models;
using YoutubeMusicPlayer.Application.Interfaces;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace YoutubeMusicPlayer.Controllers;

public class HomeController : Controller
{
    private readonly IYoutubeService _youtubeService;
    private readonly ISongService _songService;
    private readonly IInteractionService _interactionService;
    private readonly IServiceScopeFactory _scopeFactory;

    public HomeController(IYoutubeService youtubeService, ISongService songService, IInteractionService interactionService, IServiceScopeFactory scopeFactory)
    {
        _youtubeService = youtubeService;
        _songService = songService;
        _interactionService = interactionService;
        _scopeFactory = scopeFactory;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeViewModel
        {
            NhacTre = await GetSectionAsync("Nhạc trẻ hay nhất 2024"),
            NhacRemix = await GetSectionAsync("Nhạc remix sôi động 2024"),
            NhacTikTok = await GetSectionAsync("Nhạc TikTok hot nhất"),
            NhacShorts = await GetSectionAsync("Nhạc Shorts trending"),
            NhacPop = await GetSectionAsync("Nhạc Pop Việt hay nhất"),
            NhacBallad = await GetSectionAsync("Nhạc Ballad tâm trạng"),
            NhacClassic = await GetSectionAsync("Nhạc cổ điển Classic hay nhất"),
            NhacKPop = await GetSectionAsync("Nhạc K-Pop hot nhất"),
            NhacUSUK = await GetSectionAsync("Nhạc US-UK hay nhất")
        };

        if (User.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int userId))
        {
            var recentSearches = await _interactionService.GetRecentSearchHistoryAsync(userId, 5);
            model.RecentSearches = recentSearches;

            var recentSongsIds = await _interactionService.GetRecentListeningHistoryAsync(userId, 10);
            var recentSongs = new List<YoutubeVideoDetails>();
            foreach (var songId in recentSongsIds)
            {
                var songDto = await _songService.GetSongByIdAsync(songId);
                if (songDto != null && !string.IsNullOrEmpty(songDto.YoutubeVideoId))
                {
                    recentSongs.Add(new YoutubeVideoDetails
                    {
                        YoutubeVideoId = songDto.YoutubeVideoId,
                        Title = songDto.Title,
                        AuthorName = "Nhạc Của Bạn",
                        ThumbnailUrl = songDto.ThumbnailUrl ?? $"https://img.youtube.com/vi/{songDto.YoutubeVideoId}/hqdefault.jpg",
                        Duration = songDto.Duration.HasValue ? TimeSpan.FromSeconds(songDto.Duration.Value) : TimeSpan.Zero
                    });
                }
            }
            model.RecentListened = recentSongs;
        }

        return View(model);
    }

    private async Task<IEnumerable<YoutubeVideoDetails>> GetSectionAsync(string query)
    {
        try {
            return (await _youtubeService.SearchVideosAsync(query)).Take(10);
        } catch {
            return new List<YoutubeVideoDetails>();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Search(string query)
    {
        if (string.IsNullOrEmpty(query)) return BadRequest("Search query cannot be empty.");

        try
        {
            // Record search history if user is logged in
            if (User.Identity?.IsAuthenticated == true)
            {
                if (int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int userId))
                {
                    await _interactionService.RecordSearchHistoryAsync(userId, query);
                }
            }

            var results = await _youtubeService.SearchVideosAsync(query);
            var searchResponses = results.Select(v => new
            {
                Title = v.Title,
                Url = "https://youtube.com/watch?v=" + v.YoutubeVideoId,
                Author = v.AuthorName,
                Thumbnail = v.ThumbnailUrl,
                Duration = v.Duration?.ToString(@"mm\:ss")
            });
            return Json(searchResponses);
        }
        catch (Exception ex)
        {
            return BadRequest("Search failed: " + ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStreamUrl(string videoUrl)
    {
        if (string.IsNullOrEmpty(videoUrl)) return BadRequest("URL cannot be empty.");

        try
        {
            // Lấy ra YoutubeVideoId
            string youtubeId = string.Empty;
            if (videoUrl.Contains("v=")) youtubeId = videoUrl.Split("v=").Last().Split("&").First();
            else if (videoUrl.Contains("youtu.be/")) youtubeId = videoUrl.Split("youtu.be/").Last().Split("?").First();
            
            // Nếu người dùng đăng nhập, lưu lại lịch sử nghe
            string? userIdClaim = User.Identity?.IsAuthenticated == true 
                                ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                                : null;

            // PERFORMANCE: Background import the metadata to avoid blocking the stream URL response
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var bgSongService = scope.ServiceProvider.GetRequiredService<ISongService>();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<YoutubeMusicPlayer.Domain.Interfaces.IUnitOfWork>();
                    var interactionService = scope.ServiceProvider.GetRequiredService<IInteractionService>();
                    
                    // Import first
                    await bgSongService.ImportFromYoutubeAsync(videoUrl);

                    // Once imported/assured it exists, log history
                    if (!string.IsNullOrEmpty(userIdClaim) && !string.IsNullOrEmpty(youtubeId) && int.TryParse(userIdClaim, out int userId))
                    {
                        var song = await unitOfWork.Repository<YoutubeMusicPlayer.Domain.Entities.Song>().FirstOrDefaultAsync(s => s.YoutubeVideoId == youtubeId);
                        if (song != null)
                        {
                            await interactionService.RecordListeningHistoryAsync(userId, song.SongId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Background Import Error: " + ex.Message);
                }
            });

            var streamUrl = await _youtubeService.GetAudioStreamUrlAsync(videoUrl);
            return Json(new { streamUrl });
        }
        catch (Exception ex)
        {
            return BadRequest("Could not fetch stream: " + ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetRecommendations(string videoId)
    {
        if (string.IsNullOrEmpty(videoId)) return BadRequest("VideoId cannot be empty.");
        try
        {
            var results = await _youtubeService.GetRelatedVideosAsync(videoId);
            return Json(results);
        }
        catch (Exception ex)
        {
            return BadRequest("Could not fetch recommendations: " + ex.Message);
        }
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
