using Microsoft.AspNetCore.Mvc;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Models;
using System.Diagnostics;
using YoutubeMusicPlayer.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Controllers;

public class HomeController : Controller
{
    private readonly IYoutubeService _youtubeService;
    private readonly ISongService _songService;
    private readonly IRecommendationService _recommendationService;
    private readonly IInteractionService _interactionService;
    private readonly IBackgroundQueue _backgroundQueue;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IArtistService _artistService;
    private readonly IGenreService _genreService;
    private readonly IAlbumService _albumService;
    private readonly ISpotifyService _spotifyService;

    public HomeController(
        IYoutubeService youtubeService, 
        ISongService songService,
        IRecommendationService recommendationService,
        IInteractionService interactionService,
        IBackgroundQueue backgroundQueue,
        ISubscriptionService subscriptionService,
        IArtistService artistService,
        IGenreService genreService,
        IAlbumService albumService,
        ISpotifyService spotifyService)
    {
        _youtubeService = youtubeService;
        _songService = songService;
        _recommendationService = recommendationService;
        _interactionService = interactionService;
        _backgroundQueue = backgroundQueue;
        _subscriptionService = subscriptionService;
        _artistService = artistService;
        _genreService = genreService;
        _albumService = albumService;
        _spotifyService = spotifyService;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeViewModel();
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id))
        {
            userId = id;
        }

        // 1. FAST METADATA
        int hour = DateTime.Now.Hour;
        model.Greeting = hour < 12 ? "Chào buổi sáng" : (hour < 18 ? "Chào buổi chiều" : "Chào buổi tối");
        
        // Sequential DB queries (DbContext is not thread-safe)
        model.TopArtists = (await _artistService.GetPaginatedArtistsAsync(1, 12)).Artists.ToList();
        model.Genres = (await _genreService.GetAllGenresAsync()).ToList();

        if (userId.HasValue)
        {
            model.FollowedArtists = await _artistService.GetFollowedArtistsAsync(userId.Value);
            var recentHistoryIds = await _interactionService.GetRecentListeningHistoryAsync(userId.Value, 6);
            
            if (recentHistoryIds != null && recentHistoryIds.Any())
            {
                var historySongs = await _songService.GetSongsByIdsAsync(recentHistoryIds);
                model.RecentListened = historySongs.Select(s => new YoutubeVideoDetails {
                    YoutubeVideoId = s.YoutubeVideoId,
                    Title = s.Title,
                    ThumbnailUrl = s.ThumbnailUrl,
                    AuthorName = s.AuthorName ?? "Nghệ sĩ"
                }).ToList();
            }
        }

        // Heavy IO (Trending, Albums, Moods) are now handled via AJAX in the Index.cshtml
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> GetHomeSection(string type)
    {
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id))
        {
            userId = id;
        }

        var section = new MusicSection();
        
        switch (type.ToLower())
        {
            case "trending":
                section.Title = "Thịnh hành hôm nay";
                section.Layout = "Wide";
                section.Songs = await _youtubeService.GetTrendingMusicAsync(12);
                break;
            case "topcharts_vn":
                section.Title = "BXH Việt Nam";
                section.Layout = "Square";
                section.Songs = await _recommendationService.GetTopChartsAsync("Vietnam", 12);
                break;
            case "topcharts_global":
                section.Title = "BXH Toàn cầu";
                section.Layout = "Square";
                section.Songs = await _recommendationService.GetTopChartsAsync("Global", 12);
                break;
            case "albums":
                section.Title = "Album & EP phổ biến";
                section.Layout = "Square";
                section.Albums = (await _albumService.GetTrendingAlbumsAsync(12)).Take(12);
                break;
            case "dailymix":
            case "mix1":
                section.Title = "Hỗn hợp dành cho bạn";
                if (userId.HasValue) 
                {
                    section.Songs = (await _recommendationService.GetDailyMixVariantAsync(userId.Value, 0)).Take(10);
                }
                else 
                {
                    section.Songs = await _recommendationService.GetTopChartsAsync("Vietnam", 10);
                }
                break;
            case "mix2":
                if (!userId.HasValue) return Content("");
                section.Title = "Khám phá mới";
                section.Songs = (await _recommendationService.GetDailyMixVariantAsync(userId.Value, 1)).Take(10);
                break;
            case "mix3":
                if (!userId.HasValue) return Content("");
                section.Title = "Giai điệu yêu thích";
                section.Songs = (await _recommendationService.GetDailyMixVariantAsync(userId.Value, 2)).Take(10);
                break;
            case "contextual":
                string? artist = null;
                if (userId.HasValue) 
                {
                    var history = await _interactionService.GetRecentListeningHistoryAsync(userId.Value, 1);
                    if (history != null && history.Any()) {
                        var song = (await _songService.GetSongsByIdsAsync(history)).FirstOrDefault();
                        artist = song?.AuthorName;
                    }
                }
                
                var contextual = userId.HasValue 
                                 ? await _recommendationService.GetBecauseYouListenedToAsync(userId.Value, artist)
                                 : await _youtubeService.GetTrendingMusicAsync(10);

                if (contextual == null || !contextual.Any()) return Content("");
                section.Title = contextual.First().SectionTitle ?? "Gợi ý dành cho bạn";
                section.Songs = contextual.Take(12);
                break;
            case "focus":
                section.Title = "Tập trung làm việc";
                section.Songs = await _recommendationService.GetMoodMusicAsync("focus", 10);
                break;
            case "chill":
                section.Title = "Giai điệu Chill";
                section.Songs = await _recommendationService.GetMoodMusicAsync("chill", 10);
                break;
            case "sad":
                section.Title = "Tâm trạng";
                section.Songs = await _recommendationService.GetMoodMusicAsync("sad", 10);
                break;
            default:
                return BadRequest();
        }

        if ((section.Songs == null || !section.Songs.Any()) && (section.Albums == null || !section.Albums.Any()))
            return Content("");

        return PartialView("_HomeSection", section);
    }

    [HttpGet]
    public async Task<IActionResult> Search(string query)
    {
        if (string.IsNullOrEmpty(query)) return BadRequest("Search query cannot be empty.");
        try
        {
            if (User.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                await _interactionService.RecordSearchHistoryAsync(userId, query);
            }

            var ytResults = await _youtubeService.SearchVideosAsync(query);
            var albumResults = await _albumService.SearchAlbumsAsync(query);

            var finalResults = new List<object>();

            // Add albums first
            finalResults.AddRange(albumResults.Select(a => new {
                Title = a.Title,
                Author = "Album",
                Thumbnail = a.CoverImageUrl,
                Type = "Album",
                AlbumId = a.AlbumId
            }));

            // Add YouTube songs
            finalResults.AddRange(ytResults.Select(v => new {
                Title = v.Title,
                Author = v.AuthorName,
                Thumbnail = v.ThumbnailUrl,
                Type = "Song",
                VideoId = v.YoutubeVideoId
            }));

            return Json(finalResults);
        }
        catch (Exception ex)
        {
            return BadRequest("Search error: " + ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStreamUrl(string videoUrl, string? title = null, string? artist = null)
    {
        if (string.IsNullOrEmpty(videoUrl)) return BadRequest("URL cannot be empty.");

        try
        {
            string youtubeId = string.Empty;
            if (videoUrl.Contains("v=")) youtubeId = videoUrl.Split("v=").Last().Split("&").First();
            else if (videoUrl.Contains("youtu.be/")) youtubeId = videoUrl.Split("youtu.be/").Last().Split("?").First();
            
            string? userIdStr = User.Identity?.IsAuthenticated == true 
                                ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                                : null;
            int? currentUserId = null;
            if (userIdStr != null && int.TryParse(userIdStr, out int parsedId)) currentUserId = parsedId;

            var song = await _songService.GetOrCreateByYoutubeIdAsync(youtubeId);
            if (song != null)
            {
                if (song.IsPremiumOnly)
                {
                    if (!currentUserId.HasValue || !(await _subscriptionService.IsUserPremiumAsync(currentUserId.Value)))
                    {
                        return Json(new { error = "PremiumRequired", message = "Đây là bài hát dành cho hội viên Premium." });
                    }
                }

                if (song.IsExplicit)
                {
                    if (currentUserId.HasValue)
                    {
                        var user = await HttpContext.RequestServices.GetRequiredService<IAuthService>().GetUserByIdAsync(currentUserId.Value);
                        if (user != null && user.DateOfBirth.HasValue)
                        {
                            int age = DateTime.UtcNow.Year - user.DateOfBirth.Value.Year;
                            if (user.DateOfBirth.Value.AddYears(age) > DateTime.UtcNow) age--;
                            
                            if (age < 18)
                            {
                                return Json(new { error = "AgeRestricted", message = "Nội dung này không phù hợp với lứa tuổi của bạn." });
                            }
                        }
                    }
                }
            }

            _backgroundQueue.QueueBackgroundWorkItem(async (sp) =>
            {
                var interactionService = sp.GetRequiredService<IInteractionService>();
                if (currentUserId.HasValue && song != null)
                {
                    await interactionService.RecordListeningHistoryAsync(currentUserId.Value, song.SongId);
                }
            });

            var streamUrl = await _youtubeService.GetAudioStreamUrlAsync(videoUrl, title, artist);
            bool isLiked = false;
            if (currentUserId.HasValue && song != null)
            {
                isLiked = await _interactionService.IsSongLikedAsync(currentUserId.Value, song.SongId);
            }

            return Json(new { streamUrl, songId = song?.SongId, isLiked });
        }
        catch (Exception ex)
        {
            return BadRequest("Playback error: " + ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetRecommendations(string videoId)
    {
        if (string.IsNullOrEmpty(videoId)) return BadRequest("ID required.");
        try
        {
            int? userId = (User.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int id)) ? id : null;
            var results = await _recommendationService.GetSmartDiscoveryAsync(videoId, userId);
            return Json(results);
        }
        catch (Exception ex) { return BadRequest("Error: " + ex.Message); }
    }

    [HttpGet]
    public async Task<IActionResult> GetArtistSongs(int artistId)
    {
        var artist = await _artistService.GetArtistByIdAsync(artistId, 1, 100);
        if (artist == null) return NotFound();
        return Json(artist.TopSongs.Select(s => new {
            YoutubeVideoId = s.YoutubeVideoId,
            Title = s.Title,
            AuthorName = artist.Name,
            ThumbnailUrl = s.ThumbnailUrl
        }));
    }

    [HttpGet]
    public async Task<IActionResult> GetRichMetadata(string videoId)
    {
        var song = await _songService.GetOrCreateByYoutubeIdAsync(videoId);
        return Json(new {
            lyrics = song?.LyricsText ?? "Lời bài hát hiện chưa khả dụng.",
            bio = song?.AuthorBio ?? "Thông tin nghệ sĩ đang được cập nhật..."
        });
    }

    [HttpGet]
    public async Task<IActionResult> Discovery(string tag, bool json = false)
    {
        if (string.IsNullOrEmpty(tag)) return RedirectToAction("Index");
        
        IEnumerable<YoutubeVideoDetails> results;
        try {
            // Priority 1: Mood-based recommendation logic
            var moodSongs = await _recommendationService.GetMoodMusicAsync(tag.ToLower(), 24);
            if (moodSongs != null && moodSongs.Any()) {
                results = moodSongs;
            } else {
                // Priority 2: Spotify-based metadata search
                var spotifyTracks = await _spotifyService.SearchTracksAsync(tag, 20);
                var tasks = spotifyTracks.Select(async s => {
                    var searchResult = await _youtubeService.SearchVideosAsync($"{s.ArtistName} {s.TrackName} official", 1);
                    var v = searchResult.FirstOrDefault();
                    if (v == null) return null;
                    return new YoutubeVideoDetails {
                        YoutubeVideoId = v.YoutubeVideoId,
                        Title = s.TrackName,
                        AuthorName = s.ArtistName,
                        ThumbnailUrl = !string.IsNullOrEmpty(s.AlbumImageUrl) ? s.AlbumImageUrl : v.ThumbnailUrl
                    };
                }).ToList();
                results = (await Task.WhenAll(tasks)).Where(x => x != null).Cast<YoutubeVideoDetails>().ToList();
            }
        } catch { results = new List<YoutubeVideoDetails>(); }

        if (json) return Json(results);

        ViewBag.Tag = tag;
        return View(results);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
