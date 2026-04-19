using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
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
    private readonly IMemoryCache _cache;
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
        IMemoryCache cache,
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
        _cache = cache;
        _logger = logger;
    }

    public async Task<HomeViewModel> BuildHomeViewModelAsync(int? userId, string? userName = null)
    {
        string cacheKey = $"home_vm_{userId ?? 0}";
        if (_cache.TryGetValue(cacheKey, out HomeViewModel? cachedModel) && cachedModel != null)
        {
            _logger.LogInformation("[HOME-FACADE] Cache HIT for HomeViewModel. User: {User}", userName ?? "Guest");
            return cachedModel;
        }

        var model = new HomeViewModel();
        
        // Dynamic Greeting Logic
        int hour = DateTime.Now.Hour;
        string greetingBase = hour switch
        {
            >= 5 and < 11 => "Chào buổi sáng",
            >= 11 and < 13 => "Chào buổi trưa",
            >= 13 and < 18 => "Chào buổi chiều",
            >= 18 and < 22 => "Chào buổi tối",
            _ => "Chào ban đêm"
        };

        if (!string.IsNullOrEmpty(userName)) greetingBase += $", {userName}";
        model.Greeting = greetingBase;

        // FIX PERF: Load Genres + Artists song song thay vì tuần tự
        // Hai bước này hoàn toàn độc lập nhau
        var genresTask = Task.Run(async () => {
            try {
                using var scope = _scopeFactory.CreateScope();
                var genreSvc = scope.ServiceProvider.GetRequiredService<IGenreService>();
                var result = await genreSvc.GetAllGenresAsync();
                return result.OrderBy(_ => Random.Shared.Next()).ToList();
            } catch (Exception ex) {
                _logger.LogWarning("[HOME-FACADE] Failed to load genres: {Msg}", ex.Message);
                return new List<YoutubeMusicPlayer.Application.DTOs.GenreDto>();
            }
        });

        var artistsTask = Task.Run(async () => {
            try {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var artistSvc = scope.ServiceProvider.GetRequiredService<IArtistService>();
                var result = await artistSvc.GetPaginatedArtistsAsync(1, 50);
                return result.Artists.OrderBy(_ => Random.Shared.Next()).Take(12).ToList();
            } catch (Exception ex) {
                _logger.LogError(ex, "[HOME-FACADE] Error loading Top Artists. Falling back to YouTube Trending.");
                try {
                    var trending = await _youtubeService.GetTrendingMusicAsync(6);
                    return trending.Select(t => new YoutubeMusicPlayer.Application.DTOs.ArtistDto { Name = t.AuthorName ?? "Trending", AvatarUrl = t.ThumbnailUrl }).ToList();
                } catch { return new List<YoutubeMusicPlayer.Application.DTOs.ArtistDto>(); }
            }
        });

        await Task.WhenAll(genresTask, artistsTask);
        model.Genres = await genresTask;
        model.TopArtists = await artistsTask;

        // 3. User Personalization (Isolated Scope)
        if (userId.HasValue)
        {
            try {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var interactionSvc = scope.ServiceProvider.GetRequiredService<IInteractionService>();
                var songSvc = scope.ServiceProvider.GetRequiredService<ISongService>();
                var artistSvc = scope.ServiceProvider.GetRequiredService<IArtistService>();

                model.FollowedArtists = await artistSvc.GetFollowedArtistsAsync(userId.Value);

                var historyIds = await interactionSvc.GetRecentListeningHistoryAsync(userId.Value, 6);
                if (historyIds != null && historyIds.Any()) {
                    var historySongs = await songSvc.GetSongsByIdsAsync(historyIds);
                    model.RecentListened = historySongs.Select(s => new YoutubeVideoDetails {
                        YoutubeVideoId = s.YoutubeVideoId,
                        SongId = s.SongId, // Pass SongId to frontend for instant mapping
                        Title = s.Title,
                        ThumbnailUrl = s.ThumbnailUrl,
                        AuthorName = s.AuthorName ?? "Nghệ sĩ"
                    }).ToList();
                } else {
                    model.RecentListened = (await _youtubeService.GetTrendingMusicAsync(6)).ToList();
                }
            } catch {
                try { model.RecentListened = (await _youtubeService.GetTrendingMusicAsync(6)).ToList(); } catch { }
            }
        }

        // Cache entire ViewModel for 5 minutes
        _cache.Set(cacheKey, model, TimeSpan.FromMinutes(5));

        return model;
    }

    public async Task<MusicSection?> GetHomeSectionAsync(string type, int? userId, bool refresh = false)
    {
        // FIX Bug 1: Cache check ở đầu method để tránh DB query lặp lại
        bool isUserSpecific = !IsSharedSection(type) && userId.HasValue;
        string cacheKey = isUserSpecific
            ? $"section_{type.ToLower()}_{userId!.Value}"
            : $"section_{type.ToLower()}_shared";

        if (!refresh && _cache.TryGetValue(cacheKey, out MusicSection? cached) && cached != null)
        {
            _logger.LogInformation("[HOME-FACADE] Cache HIT for section: {Type}", type);
            return cached;
        }

        var section = new MusicSection();
        
        switch (type.ToLower())
        {
            case SectionTypes.Trending:
                section.Title = "Thịnh hành hôm nay";
                section.Layout = "Wide";
                using (var scope = _scopeFactory.CreateScope())
                {
                    var youtubeSvc = scope.ServiceProvider.GetRequiredService<IYoutubeService>();
                    var trendingSongs = (await youtubeSvc.GetTrendingMusicAsync(10, refresh)).ToList();
                    section.Songs = trendingSongs;

                    // FIX PERF: Loại bỏ stream URL pre-fetch khỏi scope này
                    // (scope sẽ bị dispose trước khi Task.Run hoàn thành → ObjectDisposedException)
                    // Stream URL warming được xử lý bởi CacheWarmupService hoặc lazy-load khi user play
                }
                break;
            case SectionTypes.Albums:
                section.Title = "Album & EP phổ biến";
                section.Layout = "Square";
                using (var scope = _scopeFactory.CreateScope())
                {
                    var albumSvc = scope.ServiceProvider.GetRequiredService<IAlbumService>();
                    section.Albums = (await albumSvc.GetTrendingAlbumsAsync(10)).Take(10);
                }
                break;
            case SectionTypes.DailyMix:
            case SectionTypes.Mix1:
                section.Title = "Hỗn hợp dành cho bạn";
                using (var scope = _scopeFactory.CreateScope())
                {
                    var recommendationSvc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                    var youtubeSvc = scope.ServiceProvider.GetRequiredService<IYoutubeService>();
                    section.Songs = userId.HasValue 
                        ? (await recommendationSvc.GetDailyMixVariantAsync(userId.Value, 0, null, refresh)).Take(10)
                        : await youtubeSvc.GetTrendingMusicAsync(10, refresh);
                }
                break;
            case SectionTypes.Mix2:
                if (!userId.HasValue) return null;
                section.Title = "Khám phá mới";
                using (var scope = _scopeFactory.CreateScope())
                {
                    var recommendationSvc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                    section.Songs = (await recommendationSvc.GetDailyMixVariantAsync(userId.Value, 1, null, refresh)).Take(10);
                }
                break;
            case SectionTypes.Mix3:
                if (!userId.HasValue) return null;
                section.Title = "Giai điệu yêu thích";
                using (var scope = _scopeFactory.CreateScope())
                {
                    var recommendationSvc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                    section.Songs = (await recommendationSvc.GetDailyMixVariantAsync(userId.Value, 2, null, refresh)).Take(10);
                }
                break;
            case SectionTypes.Contextual:
                string? artist = null;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var interactionSvc = scope.ServiceProvider.GetRequiredService<IInteractionService>();
                    var songSvc = scope.ServiceProvider.GetRequiredService<ISongService>();
                    var recommendationSvc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                    var youtubeSvc = scope.ServiceProvider.GetRequiredService<IYoutubeService>();

                    if (userId.HasValue) 
                    {
                        var history = await interactionSvc.GetRecentListeningHistoryAsync(userId.Value, 1);
                        if (history != null && history.Any()) {
                            var songsList = await songSvc.GetSongsByIdsAsync(history);
                            artist = songsList.FirstOrDefault()?.AuthorName;
                        }
                    }
                    var contextual = userId.HasValue 
                                     ? await recommendationSvc.GetBecauseYouListenedToAsync(userId.Value, artist, refresh)
                                     : await youtubeSvc.GetTrendingMusicAsync(10, refresh);
                    
                    if (contextual == null || !contextual.Any()) return null;
                    section.Title = contextual.First().SectionTitle ?? "Gợi ý dành cho bạn";
                    section.Songs = contextual.Take(10);
                }
                break;
            case SectionTypes.Focus:
                section.Title = "Tập trung làm việc";
                using (var scope = _scopeFactory.CreateScope())
                {
                    var recommendationSvc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                    section.Songs = (await recommendationSvc.GetMoodMusicAsync("focus", 10, refresh));
                }
                break;
            case SectionTypes.Chill:
                section.Title = "Giai điệu Chill";
                using (var scope = _scopeFactory.CreateScope())
                {
                    var recommendationSvc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                    section.Songs = (await recommendationSvc.GetMoodMusicAsync("chill", 10, refresh));
                }
                break;
            case SectionTypes.Sad:
                section.Title = "Tâm trạng";
                using (var scope = _scopeFactory.CreateScope())
                {
                    var recommendationSvc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                    section.Songs = (await recommendationSvc.GetMoodMusicAsync("sad", 10, refresh));
                }
                break;
            case SectionTypes.Compilations:
                section.Title = "Nhạc tổng hợp";
                section.Layout = "Wide";
                using (var scope = _scopeFactory.CreateScope())
                {
                    var recommendationSvc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                    section.Songs = (await recommendationSvc.GetCompilationsAsync(10, refresh));
                }
                break;
            default:
                return null;
        }

        if ((section.Songs == null || !section.Songs.Any()) && (section.Albums == null || !section.Albums.Any()))
            return null;

        // Level 3: Active Cache Mapping - Populate YoutubeId -> SongId for all songs in this section
        if (section.Songs != null) {
            foreach (var song in section.Songs) {
                if (!string.IsNullOrEmpty(song.YoutubeVideoId) && song.SongId.HasValue) {
                    string mappingCacheKey = $"yt_to_songid_{song.YoutubeVideoId}";
                    
                    // Create a lightweight DTO for mapping
                    var dto = new SongDto { 
                        SongId = song.SongId.Value, 
                        YoutubeVideoId = song.YoutubeVideoId,
                        Title = song.Title,
                        AuthorName = song.AuthorName,
                        ThumbnailUrl = song.ThumbnailUrl
                    };
                    _cache.Set(mappingCacheKey, dto, TimeSpan.FromMinutes(30));
                }
            }
        }

        // FIX Bug 1: Cache section result để tránh DB query lặp lại
        // Shared sections (Trending, Albums, Focus, Chill, Sad, Compilations): TTL 10 phút
        // User-specific sections (DailyMix, Mix1, Mix2, Mix3, Contextual): TTL 5 phút
        var ttl = IsSharedSection(type) ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(5);
        _cache.Set(cacheKey, section, ttl);
        _logger.LogInformation("[HOME-FACADE] Cache SET for section: {Type} (TTL: {TTL}min)", type, ttl.TotalMinutes);

        return section;
    }

    /// <summary>
    /// Xác định section có phải là shared (không phụ thuộc userId) hay không.
    /// Shared sections được cache chung cho mọi user với TTL dài hơn (10 phút).
    /// User-specific sections được cache riêng theo userId với TTL ngắn hơn (5 phút).
    /// </summary>
    private static bool IsSharedSection(string type) =>
        type.ToLower() is SectionTypes.Trending or SectionTypes.Albums
            or SectionTypes.Focus or SectionTypes.Chill
            or SectionTypes.Sad or SectionTypes.Compilations;

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
        if (deezerArtists != null && deezerArtists.Any())
        {
            using var scope = _scopeFactory.CreateScope();
            var artistSvc = scope.ServiceProvider.GetRequiredService<IArtistService>();
            
            foreach (var da in deezerArtists)
            {
                if (!internalArtists.Any(a => a.Name.ToLower() == da.Name.ToLower()))
                {
                    var stub = await artistSvc.GetOrCreateArtistStubAsync(da.Name, da.ImageUrl);
                    if (stub != null) internalArtists.Add(stub);
                }
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

        string cacheKey = $"artist_songs_{name.ToLower().Replace(" ", "_")}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<SongDto>? cachedSongs) && cachedSongs != null)
        {
            _logger.LogInformation("[HOME-FACADE] Cache HIT for artist songs: {Artist}", name);
            return cachedSongs;
        }

        // OPTIMIZED: Single scope, single DB round-trip via direct join query
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        
        // Find artist by name (case-insensitive)
        var artist = await uow.Repository<Domain.Entities.Artist>().Query()
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.Name.ToLower().Contains(name.ToLower()))
            .FirstOrDefaultAsync();

        if (artist == null) return Enumerable.Empty<SongDto>();

        // Direct query: get songs through SongArtist join in ONE trip
        var songs = await uow.Repository<Domain.Entities.Song>().Query()
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.SongArtists.Any(sa => sa.ArtistId == artist.ArtistId))
            .OrderByDescending(s => s.PlayCount)
            .Take(20)
            .Select(s => new SongDto
            {
                SongId = s.SongId,
                Title = s.Title,
                YoutubeVideoId = s.YoutubeVideoId,
                ThumbnailUrl = s.ThumbnailUrl,
                Duration = s.Duration,
                PlayCount = s.PlayCount,
                AuthorName = s.SongArtists.Select(sa => sa.Artist.Name).FirstOrDefault() ?? artist.Name
            })
            .ToListAsync();

        // Per-artist caching (30 minutes)
        _cache.Set(cacheKey, (IEnumerable<SongDto>)songs, TimeSpan.FromMinutes(30));
        
        return songs;
    }
    public async Task<IEnumerable<YoutubeVideoDetails>> GetDiscoverySongsAsync(string tag, int page, int limit)
    {
        string cacheKey = $"discovery_v2_{tag.ToLower().Replace(" ", "_")}";
        if (!_cache.TryGetValue(cacheKey, out List<YoutubeVideoDetails>? pool) || pool == null)
        {
            // 1. Fetch a larger pool from recommendation service (already optimized to return 80+)
            var fullPool = await _recommendationService.GetMoodMusicAsync(tag, 100);
            pool = fullPool.ToList();
            
            // Cache discovery pool for 15 minutes
            _cache.Set(cacheKey, pool, TimeSpan.FromMinutes(15));
        }
        
        // 2. Perform slicing based on page
        int skip = (page - 1) * limit;
        return pool.Skip(skip).Take(limit);
    }

    public async Task<long> GetSongPlayCountAsync(int songId)
    {
        // Kiểm tra cache trước để tránh DB query không cần thiết
        string cacheKey = $"song_playcount_{songId}";
        if (_cache.TryGetValue(cacheKey, out long cachedCount))
            return cachedCount;

        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var playCount = await uow.Repository<Domain.Entities.Song>().Query()
            .AsNoTracking()
            .Where(s => s.SongId == songId && !s.IsDeleted)
            .Select(s => s.PlayCount)
            .FirstOrDefaultAsync();

        // Cache ngắn (2 phút) vì PlayCount thay đổi khi user nghe
        _cache.Set(cacheKey, playCount, TimeSpan.FromMinutes(2));
        return playCount;
    }
}
