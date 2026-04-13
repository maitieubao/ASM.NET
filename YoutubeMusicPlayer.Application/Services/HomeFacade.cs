using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace YoutubeMusicPlayer.Application.Services;

public class HomeFacade : IHomeFacade
{
    private readonly IArtistService _artistService;
    private readonly IGenreService _genreService;
    private readonly IInteractionService _interactionService;
    private readonly ISongService _songService;
    private readonly IYoutubeService _youtubeService;
    private readonly IAlbumService _albumService;
    private readonly IRecommendationService _recommendationService;
    private readonly IDeezerService _deezerService;
    private readonly IITunesService _itunesService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HomeFacade> _logger;

    public HomeFacade(
        IArtistService artistService,
        IGenreService genreService,
        IInteractionService interactionService,
        ISongService songService,
        IYoutubeService youtubeService,
        IAlbumService albumService,
        IRecommendationService recommendationService,
        IDeezerService deezerService,
        IITunesService itunesService,
        IServiceScopeFactory scopeFactory,
        ILogger<HomeFacade> logger)
    {
        _artistService = artistService;
        _genreService = genreService;
        _interactionService = interactionService;
        _songService = songService;
        _youtubeService = youtubeService;
        _albumService = albumService;
        _recommendationService = recommendationService;
        _deezerService = deezerService;
        _itunesService = itunesService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<HomeViewModel> BuildHomeViewModelAsync(int? userId)
    {
        var model = new HomeViewModel();
        int hour = DateTime.Now.Hour;
        model.Greeting = hour < 12 ? "Chào buổi sáng" : (hour < 18 ? "Chào buổi chiều" : "Chào buổi tối");

        // FIRE ALL TASKS CONCURRENTLY
        var tasks = new List<Task>();

        // Task 1: Artists (Isolated Scope)
        var artistTask = Task.Run(async () => {
            using var scope = _scopeFactory.CreateScope();
            var artistSvc = scope.ServiceProvider.GetRequiredService<IArtistService>();
            var result = await artistSvc.GetPaginatedArtistsAsync(1, 50);
            model.TopArtists = result.Artists.OrderBy(_ => Random.Shared.Next()).Take(12).ToList();
        });
        tasks.Add(artistTask);

        // Task 2: Genres (Isolated Scope)
        var genreTask = Task.Run(async () => {
            using var scope = _scopeFactory.CreateScope();
            var genreSvc = scope.ServiceProvider.GetRequiredService<IGenreService>();
            var result = await genreSvc.GetAllGenresAsync();
            model.Genres = result.OrderBy(_ => Random.Shared.Next()).ToList();
        });
        tasks.Add(genreTask);

        // Task 3: User Personalization (Isolated Scopes)
        if (userId.HasValue)
        {
            // 3.1 Followed Artists
            var followedTask = Task.Run(async () => {
                using var scope = _scopeFactory.CreateScope();
                var artistSvc = scope.ServiceProvider.GetRequiredService<IArtistService>();
                model.FollowedArtists = await artistSvc.GetFollowedArtistsAsync(userId.Value);
            });
            tasks.Add(followedTask);

            // 3.2 Listening History & Recent Listened
            var historyTask = Task.Run(async () => {
                using var scope = _scopeFactory.CreateScope();
                var interactionSvc = scope.ServiceProvider.GetRequiredService<IInteractionService>();
                var songSvc = scope.ServiceProvider.GetRequiredService<ISongService>();

                var historyIds = await interactionSvc.GetRecentListeningHistoryAsync(userId.Value, 6);
                if (historyIds != null && historyIds.Any())
                {
                    var historySongs = await songSvc.GetSongsByIdsAsync(historyIds);
                    model.RecentListened = historySongs.Select(s => new YoutubeVideoDetails {
                        YoutubeVideoId = s.YoutubeVideoId,
                        Title = s.Title,
                        ThumbnailUrl = s.ThumbnailUrl,
                        AuthorName = s.AuthorName ?? "Nghệ sĩ"
                    }).ToList();
                }
            });
            tasks.Add(historyTask);
        }

        await Task.WhenAll(tasks);
        return model;
    }

    public async Task<MusicSection?> GetHomeSectionAsync(string type, int? userId, bool refresh = false)
    {
        var section = new MusicSection();
        
        switch (type.ToLower())
        {
            case SectionTypes.Trending:
                section.Title = "Thịnh hành hôm nay";
                section.Layout = "Wide";
                section.Songs = (await _youtubeService.GetTrendingMusicAsync(10, refresh));
                break;
            case SectionTypes.Albums:
                section.Title = "Album & EP phổ biến";
                section.Layout = "Square";
                section.Albums = (await _albumService.GetTrendingAlbumsAsync(10)).Take(10);
                break;
            case SectionTypes.DailyMix:
            case SectionTypes.Mix1:
                section.Title = "Hỗn hợp dành cho bạn";
                section.Songs = userId.HasValue 
                    ? (await _recommendationService.GetDailyMixVariantAsync(userId.Value, 0, null, refresh)).Take(10)
                    : await _youtubeService.GetTrendingMusicAsync(10, refresh);
                break;
            case SectionTypes.Mix2:
                if (!userId.HasValue) return null;
                section.Title = "Khám phá mới";
                section.Songs = (await _recommendationService.GetDailyMixVariantAsync(userId.Value, 1, null, refresh)).Take(10);
                break;
            case SectionTypes.Mix3:
                if (!userId.HasValue) return null;
                section.Title = "Giai điệu yêu thích";
                section.Songs = (await _recommendationService.GetDailyMixVariantAsync(userId.Value, 2, null, refresh)).Take(10);
                break;
            case SectionTypes.Contextual:
                string? artist = null;
                if (userId.HasValue) 
                {
                    var history = await _interactionService.GetRecentListeningHistoryAsync(userId.Value, 1);
                    if (history != null && history.Any()) {
                        var songs = await _songService.GetSongsByIdsAsync(history);
                        artist = songs.FirstOrDefault()?.AuthorName;
                    }
                }
                var contextual = userId.HasValue 
                                 ? await _recommendationService.GetBecauseYouListenedToAsync(userId.Value, artist, refresh)
                                 : await _youtubeService.GetTrendingMusicAsync(10, refresh);
                if (contextual == null || !contextual.Any()) return null;
                section.Title = contextual.First().SectionTitle ?? "Gợi ý dành cho bạn";
                section.Songs = contextual.Take(10);
                break;
            case SectionTypes.Focus:
                section.Title = "Tập trung làm việc";
                section.Songs = (await _recommendationService.GetMoodMusicAsync("focus", 10, refresh));
                break;
            case SectionTypes.Chill:
                section.Title = "Giai điệu Chill";
                section.Songs = (await _recommendationService.GetMoodMusicAsync("chill", 10, refresh));
                break;
            case SectionTypes.Sad:
                section.Title = "Tâm trạng";
                section.Songs = (await _recommendationService.GetMoodMusicAsync("sad", 10, refresh));
                break;
            case SectionTypes.Compilations:
                section.Title = "Nhạc tổng hợp";
                section.Layout = "Wide";
                section.Songs = (await _recommendationService.GetCompilationsAsync(10, refresh));
                break;
            default:
                return null;
        }

        if ((section.Songs == null || !section.Songs.Any()) && (section.Albums == null || !section.Albums.Any()))
            return null;

        return section;
    }

    public async Task<List<SearchResultDto>> SearchAllAsync(string query, int? userId)
    {
        if (userId.HasValue)
        {
            // Record search history in background with isolated scope to prevent DbContext collisions
            _ = Task.Run(async () => {
                try {
                    using var scope = _scopeFactory.CreateScope();
                    var interactionSvc = scope.ServiceProvider.GetRequiredService<IInteractionService>();
                    await interactionSvc.RecordSearchHistoryAsync(userId.Value, query);
                } catch { /* Silent fail for history tracking */ }
            });
        }

        _logger.LogInformation("[SEARCH] Initiating ULTIMATE PARALLEL search for query: {Query}", query);
        
        // 1. YouTube Search
        var ytTask = _youtubeService.SearchVideosAsync(query);
        
        // 2. Internal Search (Isolated Scopes for Artist and Album)
        var artistSearchTask = Task.Run(async () => {
            using var scope = _scopeFactory.CreateScope();
            var artistSvc = scope.ServiceProvider.GetRequiredService<IArtistService>();
            return await artistSvc.SearchArtistsAsync(query);
        });

        var albumSearchTask = Task.Run(async () => {
            using var scope = _scopeFactory.CreateScope();
            var albumSvc = scope.ServiceProvider.GetRequiredService<IAlbumService>();
            return await albumSvc.SearchAlbumsAsync(query);
        });

        // 3. External Search (Deezer & iTunes)
        var deezerTask = _deezerService.SearchAlbumsAsync(query, 5);
        var deezerArtistTask = _deezerService.SearchArtistsAsync(query, 3);
        var itunesTask = _itunesService.SearchAlbumsAsync(query, 5);

        // WAIT FOR EVERYTHING AT ONCE
        await Task.WhenAll(ytTask, artistSearchTask, albumSearchTask, deezerTask, deezerArtistTask, itunesTask);

        var ytResults = await ytTask;
        var internalArtists = (await artistSearchTask).ToList();
        var internalAlbums = await albumSearchTask;
        var deezerAlbums = await deezerTask;
        var deezerArtists = await deezerArtistTask;
        var itunesAlbums = await itunesTask;

        // Ensure external artists found have a place in our system
        foreach (var da in deezerArtists)
        {
            if (!internalArtists.Any(a => a.Name.ToLower() == da.Name.ToLower()))
            {
                using var scope = _scopeFactory.CreateScope();
                var artistSvc = scope.ServiceProvider.GetRequiredService<IArtistService>();
                var stub = await artistSvc.GetOrCreateArtistStubAsync(da.Name, da.ImageUrl);
                internalArtists.Add(stub);
            }
        }

        var finalResults = new List<SearchResultDto>();

        // Add artists
        finalResults.AddRange(internalArtists.Select(a => new SearchResultDto {
            Title = a.Name,
            Author = "Nghệ sĩ",
            Thumbnail = a.AvatarUrl ?? "https://ui-avatars.com/api/?name=" + a.Name,
            Type = "Artist",
            ArtistId = a.ArtistId,
            IsVerified = a.IsVerified
        }));

        // Add internal albums
        finalResults.AddRange(internalAlbums.Take(3).Select(a => new SearchResultDto {
            Title = a.Title,
            Author = "Album",
            Thumbnail = a.CoverImageUrl ?? string.Empty,
            Type = "Album",
            AlbumId = a.AlbumId,
            Source = "Internal"
        }));

        // Add Deezer Albums (Treat Singles as Songs)
        foreach (var a in deezerAlbums)
        {
            if (a.AlbumType?.ToLower() == "single")
            {
                finalResults.Add(new SearchResultDto {
                    Title = a.Title,
                    Author = a.ArtistName ?? "Nghệ sĩ",
                    Thumbnail = a.CoverImageUrl ?? string.Empty,
                    Type = "Song",
                    ExternalId = a.DeezerId,
                    Source = "Deezer"
                });
            }
            else
            {
                finalResults.Add(new SearchResultDto {
                    Title = a.Title,
                    Author = a.ArtistName ?? "Nghệ sĩ",
                    Thumbnail = a.CoverImageUrl ?? string.Empty,
                    Type = "Album",
                    ExternalId = a.DeezerId,
                    Source = "Deezer"
                });
            }
        }

        // Add iTunes Albums
        finalResults.AddRange(itunesAlbums.Select(a => new SearchResultDto {
            Title = a.CollectionName,
            Author = a.ArtistName,
            Thumbnail = a.ArtworkUrl,
            Type = "Album", // iTunes API "album" matches are usually albums, but we could lookup tracks count if needed. For now, keep as Album or check single track info if available.
            ExternalId = a.CollectionId,
            Source = "iTunes"
        }));

        // Add songs
        finalResults.AddRange(ytResults.Select(v => new SearchResultDto {
            Title = v.Title,
            Author = v.AuthorName,
            Thumbnail = v.ThumbnailUrl ?? string.Empty,
            Type = "Song",
            VideoId = v.YoutubeVideoId
        }));

        return finalResults;
    }
    public async Task<IEnumerable<SongDto>> GetSongsByArtistAsync(string name)
    {
        if (string.IsNullOrEmpty(name)) return Enumerable.Empty<SongDto>();

        // We search for the artist by name (case-insensitive)
        var artist = await _artistService.SearchArtistsAsync(name, 1);
        var artistDto = artist.FirstOrDefault();

        if (artistDto == null) return Enumerable.Empty<SongDto>();

        // Use the existing GetArtistByIdAsync which returns top songs
        var details = await _artistService.GetArtistByIdAsync(artistDto.ArtistId);
        return details?.TopSongs ?? Enumerable.Empty<SongDto>();
    }
    public async Task<IEnumerable<YoutubeVideoDetails>> GetDiscoverySongsAsync(string tag, int page, int limit)
    {
        // 1. Fetch a larger pool from recommendation service (already optimized to return 80+)
        var pool = await _recommendationService.GetMoodMusicAsync(tag, 100);
        
        // 2. Perform slicing based on page
        // Page 1: skip 0, take 25
        // Page 2: skip 25, take 25...
        int skip = (page - 1) * limit;
        return pool.Skip(skip).Take(limit);
    }
}
