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
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<HomeViewModel> BuildHomeViewModelAsync(int? userId)
    {
        var model = new HomeViewModel();
        int hour = DateTime.Now.Hour;
        model.Greeting = hour < 12 ? "Chào buổi sáng" : (hour < 18 ? "Chào buổi chiều" : "Chào buổi tối");

        // 1. Fetch Artists and Genres (Parallel DB Queries using Scopes)
        // Note: Sequential is also fine for small data, but let's fulfill the user request for parallelism
        using (var scope1 = _scopeFactory.CreateScope())
        using (var scope2 = _scopeFactory.CreateScope())
        {
            var artistSvc = scope1.ServiceProvider.GetRequiredService<IArtistService>();
            var genreSvc = scope2.ServiceProvider.GetRequiredService<IGenreService>();

            var artistTask = artistSvc.GetPaginatedArtistsAsync(1, 50);
            var genreTask = genreSvc.GetAllGenresAsync();

            await Task.WhenAll(artistTask, genreTask);

            var random = Random.Shared;
            model.TopArtists = (await artistTask).Artists.OrderBy(_ => random.Next()).Take(12).ToList();
            model.Genres = (await genreTask).OrderBy(_ => random.Next()).ToList();
        }

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
            await _interactionService.RecordSearchHistoryAsync(userId.Value, query);
        }

        string optimizedQuery = query;
        // Search Logic: Just use the raw query for YouTube to get default behavior as requested
        
        // Parallel Search: YouTube + Parallel DB Tasks (Scope-based)
        var ytTask = _youtubeService.SearchVideosAsync(optimizedQuery);
        
        IEnumerable<ArtistDto> artistResults;
        IEnumerable<AlbumDto> albumResults;

        using (var scope1 = _scopeFactory.CreateScope())
        using (var scope2 = _scopeFactory.CreateScope())
        {
            var artistSvc = scope1.ServiceProvider.GetRequiredService<IArtistService>();
            var albumSvc = scope2.ServiceProvider.GetRequiredService<IAlbumService>();

            var artistTask = artistSvc.SearchArtistsAsync(query);
            var albumTask = albumSvc.SearchAlbumsAsync(query);

            await Task.WhenAll(ytTask, artistTask, albumTask);

            artistResults = await artistTask;
            albumResults = await albumTask;
        }

        var ytResults = await ytTask;
        var finalResults = new List<SearchResultDto>();

        // Add artists
        finalResults.AddRange(artistResults.Select(a => new SearchResultDto {
            Title = a.Name,
            Author = "Nghệ sĩ",
            Thumbnail = a.AvatarUrl ?? "https://ui-avatars.com/api/?name=" + a.Name,
            Type = "Artist",
            ArtistId = a.ArtistId,
            IsVerified = a.IsVerified
        }));

        // Add albums
        finalResults.AddRange(albumResults.Take(5).Select(a => new SearchResultDto {
            Title = a.Title,
            Author = "Album", // AlbumDto might not have AuthorName yet, use default
            Thumbnail = a.CoverImageUrl ?? string.Empty,
            Type = "Album",
            AlbumId = a.AlbumId
        }));

        // Add songs (No filtering for global search as per user request)
        finalResults.AddRange(ytResults
            .Select(v => new SearchResultDto {
                Title = v.Title,
                Author = v.AuthorName,
                Thumbnail = v.ThumbnailUrl ?? string.Empty,
                Type = "Song",
                VideoId = v.YoutubeVideoId
            }));

        return finalResults;
    }
}
