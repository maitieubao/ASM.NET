using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IUserService _userService;
    private readonly ICategoryService _categoryService;
    private readonly IGenreService _genreService;
    private readonly IPlaylistService _playlistService;
    private readonly ISongService _songService;
    private readonly IDashboardService _dashboardService;
    private readonly ICommentService _commentService;
    private readonly INotificationService _notificationService;
    private readonly IArtistService _artistService;
    private readonly IAlbumService _albumService;

    public AdminController(IUserService userService, 
                           ICategoryService categoryService, 
                           IGenreService genreService,
                           IPlaylistService playlistService,
                           ISongService songService,
                           IDashboardService dashboardService,
                           ICommentService commentService,
                           INotificationService notificationService,
                           IArtistService artistService,
                           IAlbumService albumService)
    {
        _userService = userService;
        _categoryService = categoryService;
        _genreService = genreService;
        _playlistService = playlistService;
        _songService = songService;
        _dashboardService = dashboardService;
        _commentService = commentService;
        _notificationService = notificationService;
        _artistService = artistService;
        _albumService = albumService;
    }

    // Existing methods...

    // --- NOTIFICATION MANAGEMENT ---
    public async Task<IActionResult> Notifications()
    {
        var notifications = await _notificationService.GetAllNotificationsAsync();
        return View(notifications);
    }

    [HttpPost]
    public async Task<IActionResult> SendNotification(string title, string message, int? userId)
    {
        if (userId.HasValue)
            await _notificationService.SendUserNotificationAsync(userId.Value, title, message);
        else
            await _notificationService.SendSystemNotificationAsync(title, message);
        
        return RedirectToAction(nameof(Notifications));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        await _notificationService.DeleteNotificationAsync(id);
        return RedirectToAction(nameof(Notifications));
    }

    // --- COMMENT MANAGEMENT ---
    public async Task<IActionResult> Comments()
    {
        var comments = await _commentService.GetAllCommentsAsync();
        return View(comments);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteComment(int id)
    {
        await _commentService.DeleteCommentAsync(id);
        return RedirectToAction(nameof(Comments));
    }

    // --- FEATURED PLAYLIST MANAGEMENT ---
    public async Task<IActionResult> FeaturedPlaylists()
    {
        var playlists = await _playlistService.GetFeaturedPlaylistsAsync();
        return View(playlists);
    }

    public IActionResult CreateFeaturedPlaylist() => View();

    [HttpPost]
    public async Task<IActionResult> CreateFeaturedPlaylist(YoutubeMusicPlayer.Application.DTOs.PlaylistDto dto)
    {
        if (string.IsNullOrEmpty(dto.Title)) return View(dto);
        await _playlistService.CreateFeaturedPlaylistAsync(dto.Title, dto.FeaturedType, dto.Description, dto.CoverImageUrl);
        return RedirectToAction(nameof(FeaturedPlaylists));
    }

    public async Task<IActionResult> EditFeaturedPlaylist(int id)
    {
        var p = await _playlistService.GetPlaylistByIdAsync(id);
        if (p == null) return NotFound();
        
        ViewBag.AllSongs = await _songService.GetAllSongsAsync();
        return View(p);
    }

    [HttpPost]
    public async Task<IActionResult> EditFeaturedPlaylist(YoutubeMusicPlayer.Application.DTOs.PlaylistDto dto)
    {
        if (string.IsNullOrEmpty(dto.Title)) return View(dto);
        await _playlistService.UpdatePlaylistAsync(dto);
        return RedirectToAction(nameof(FeaturedPlaylists));
    }

    [HttpPost]
    public async Task<IActionResult> AddSongToFeaturedPlaylist(int playlistId, int songId)
    {
        await _playlistService.AddSongToPlaylistAsync(playlistId, songId, null);
        return RedirectToAction(nameof(EditFeaturedPlaylist), new { id = playlistId });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveSongFromFeaturedPlaylist(int playlistId, int songId)
    {
        await _playlistService.RemoveSongFromPlaylistAsync(playlistId, songId, null);
        return RedirectToAction(nameof(EditFeaturedPlaylist), new { id = playlistId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteFeaturedPlaylist(int id)
    {
        await _playlistService.DeletePlaylistAsync(id, null);
        return RedirectToAction(nameof(FeaturedPlaylists));
    }

    // --- DASHBOARD ---
    public async Task<IActionResult> Index()
    {
        var stats = await _dashboardService.GetStatsAsync();
        return View(stats);
    }

    // --- REPORT MANAGEMENT ---
    public async Task<IActionResult> Reports()
    {
        var reports = await _dashboardService.GetAllReportsAsync();
        return View(reports);
    }

    [HttpPost]
    public async Task<IActionResult> ResolveReport(int id, bool takeAction)
    {
        await _dashboardService.ResolveReportAsync(id, takeAction);
        return RedirectToAction(nameof(Reports));
    }

    [HttpPost]
    public async Task<IActionResult> DismissReport(int id)
    {
        await _dashboardService.DismissReportAsync(id);
        return RedirectToAction(nameof(Reports));
    }

    // Existing Users methods...
    
    // --- CATEGORY MANAGEMENT ---
    public async Task<IActionResult> Categories()
    {
        var categories = await _categoryService.GetAllCategoriesAsync();
        return View(categories);
    }

    public IActionResult CreateCategory() => View();

    [HttpPost]
    public async Task<IActionResult> CreateCategory(YoutubeMusicPlayer.Application.DTOs.CategoryDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        await _categoryService.CreateCategoryAsync(dto);
        return RedirectToAction(nameof(Categories));
    }

    public async Task<IActionResult> EditCategory(int id)
    {
        var c = await _categoryService.GetCategoryByIdAsync(id);
        if (c == null) return NotFound();
        return View(c);
    }

    [HttpPost]
    public async Task<IActionResult> EditCategory(YoutubeMusicPlayer.Application.DTOs.CategoryDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        await _categoryService.UpdateCategoryAsync(dto);
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        await _categoryService.DeleteCategoryAsync(id);
        return RedirectToAction(nameof(Categories));
    }

    // --- GENRE MANAGEMENT ---
    public async Task<IActionResult> Genres()
    {
        var genres = await _genreService.GetAllGenresAsync();
        return View(genres);
    }

    public IActionResult CreateGenre() => View();

    [HttpPost]
    public async Task<IActionResult> CreateGenre(YoutubeMusicPlayer.Application.DTOs.GenreDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        await _genreService.CreateGenreAsync(dto);
        return RedirectToAction(nameof(Genres));
    }

    public async Task<IActionResult> EditGenre(int id)
    {
        var g = await _genreService.GetGenreByIdAsync(id);
        if (g == null) return NotFound();
        return View(g);
    }

    [HttpPost]
    public async Task<IActionResult> EditGenre(YoutubeMusicPlayer.Application.DTOs.GenreDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        await _genreService.UpdateGenreAsync(dto);
        return RedirectToAction(nameof(Genres));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteGenre(int id)
    {
        await _genreService.DeleteGenreAsync(id);
        return RedirectToAction(nameof(Genres));
    }

    public async Task<IActionResult> Users(string searchTerm)
    {
        var users = await _userService.SearchUsersAsync(searchTerm);
        ViewBag.SearchTerm = searchTerm;
        return View(users);
    }

    public async Task<IActionResult> UserDetails(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null) return NotFound();

        ViewBag.ListeningHistory = await _userService.GetUserListeningHistoryAsync(id);
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleUserLock(int id)
    {
        var result = await _userService.ToggleUserLockAsync(id);
        if (!result) return NotFound();
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var result = await _userService.DeleteUserAsync(id);
        if (!result) return NotFound();
        return RedirectToAction(nameof(Users));
    }

    // --- SONG MANAGEMENT ---
    public async Task<IActionResult> Songs(int page = 1, string? searchTerm = null)
    {
        int pageSize = 15;
        var (songs, totalCount) = await _songService.GetPaginatedSongsAsync(page, pageSize, searchTerm);
        
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        ViewBag.SearchTerm = searchTerm;
        ViewBag.TotalCount = totalCount;
        
        return View(songs);
    }

    public async Task<IActionResult> EditSong(int id)
    {
        var s = await _songService.GetSongByIdAsync(id);
        if (s == null) return NotFound();

        ViewBag.Genres = await _genreService.GetAllGenresAsync();
        return View(s);
    }

    [HttpPost]
    public async Task<IActionResult> EditSong(YoutubeMusicPlayer.Application.DTOs.SongDto dto)
    {
        if (string.IsNullOrEmpty(dto.Title)) return View(dto);
        await _songService.UpdateSongAsync(dto);
        return RedirectToAction(nameof(Songs));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSong(int id)
    {
        await _songService.DeleteSongAsync(id);
        return RedirectToAction(nameof(Songs));
    }

    // --- ARTIST MANAGEMENT ---
    public async Task<IActionResult> Artists(int page = 1, string? searchTerm = null)
    {
        int pageSize = 15;
        var (artists, totalCount) = await _artistService.GetPaginatedArtistsAsync(page, pageSize, searchTerm);
        
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        ViewBag.SearchTerm = searchTerm;
        ViewBag.TotalCount = totalCount;
        
        return View(artists);
    }

    public async Task<IActionResult> EditArtist(int id)
    {
        var a = await _artistService.GetArtistByIdAsync(id);
        if (a == null) return NotFound();
        return View(a);
    }

    [HttpPost]
    public async Task<IActionResult> EditArtist(YoutubeMusicPlayer.Application.DTOs.ArtistDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        await _artistService.UpdateArtistAsync(dto);
        return RedirectToAction(nameof(Artists));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteArtist(int id)
    {
        await _artistService.DeleteArtistAsync(id);
        return RedirectToAction(nameof(Artists));
    }

    [HttpPost]
    public async Task<IActionResult> RefreshArtistBio(int id)
    {
        await _artistService.RefreshArtistBioAsync(id);
        return RedirectToAction(nameof(EditArtist), new { id = id });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleArtistVerify(int id)
    {
        var artist = await _artistService.GetArtistByIdAsync(id);
        if (artist != null)
        {
            artist.IsVerified = !artist.IsVerified;
            await _artistService.UpdateArtistAsync(artist);
            return Json(new { success = true, isVerified = artist.IsVerified });
        }
        return NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> ToggleSongPremium(int id)
    {
        var song = await _songService.GetSongByIdAsync(id);
        if (song != null)
        {
            song.IsPremiumOnly = !song.IsPremiumOnly;
            await _songService.UpdateSongAsync(song);
            return Json(new { success = true, isPremium = song.IsPremiumOnly });
        }
        return NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> ToggleSongExplicit(int id)
    {
        var song = await _songService.GetSongByIdAsync(id);
        if (song != null)
        {
            song.IsExplicit = !song.IsExplicit;
            await _songService.UpdateSongAsync(song);
            return Json(new { success = true, isExplicit = song.IsExplicit });
        }
        return NotFound();
    }

    // --- ALBUM MANAGEMENT ---
    public async Task<IActionResult> Albums()
    {
        var albums = await _albumService.GetAllAlbumsAsync();
        return View(albums);
    }

    public async Task<IActionResult> EditAlbum(int id)
    {
        var a = await _albumService.GetAlbumByIdAsync(id);
        if (a == null) return NotFound();
        return View(a);
    }

    [HttpPost]
    public async Task<IActionResult> EditAlbum(YoutubeMusicPlayer.Application.DTOs.AlbumDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        await _albumService.UpdateAlbumAsync(dto);
        return RedirectToAction(nameof(Albums));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        await _albumService.DeleteAlbumAsync(id);
        return RedirectToAction(nameof(Albums));
    }
}
