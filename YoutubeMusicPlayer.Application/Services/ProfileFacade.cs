using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class ProfileFacade : IProfileFacade
{
    private readonly IUserService _userService;
    private readonly INotificationService _notificationService;
    private readonly IPlaylistService _playlistService;
    private readonly IInteractionService _interactionService;
    private readonly IArtistService _artistService;
    private readonly IServiceScopeFactory _scopeFactory;

    public ProfileFacade(IUserService userService, 
                         INotificationService notificationService, 
                         IPlaylistService playlistService, 
                         IInteractionService interactionService,
                         IArtistService artistService,
                         IServiceScopeFactory scopeFactory)
    {
        _userService = userService;
        _notificationService = notificationService;
        _playlistService = playlistService;
        _interactionService = interactionService;
        _artistService = artistService;
        _scopeFactory = scopeFactory;
    }

    public async Task<UserProfileViewModel> BuildUserProfileAsync(int userId)
    {
        // 1. Kick off tasks in parallel using ISOLATED SCOPES to prevent DbContext concurrency issues
        // Pattern adopted from HomeFacade to ensure thread safety with high performance
        
        var userTask = Task.Run(async () => {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IUserService>();
            return await svc.GetUserByIdAsync(userId);
        });

        var historyTask = Task.Run(async () => {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IUserService>();
            return await svc.GetUserListeningHistoryAsync(userId);
        });

        var notifyTask = Task.Run(async () => {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
            return await svc.GetUserNotificationsAsync(userId);
        });

        var playlistTask = Task.Run(async () => {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            return await svc.GetUserPlaylistsAsync(userId);
        });

        var genreTask = Task.Run(async () => {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IInteractionService>();
            return await svc.GetTopPreferredGenresAsync(userId);
        });

        var likedSongsTask = Task.Run(async () => {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IInteractionService>();
            return await svc.GetLikedSongIdsAsync(userId);
        });

        var followedArtistsTask = Task.Run(async () => {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IArtistService>();
            return await svc.GetFollowedArtistsAsync(userId);
        });

        // 2. Wait for all to complete
        await Task.WhenAll(userTask, historyTask, notifyTask, playlistTask, genreTask, likedSongsTask, followedArtistsTask);

        var user = await userTask;
        if (user == null) return null!;

        var history = (await historyTask) ?? new List<ListeningHistoryDto>();
        var notifications = (await notifyTask) ?? new List<NotificationDto>();
        var playlists = (await playlistTask) ?? new List<PlaylistDto>();
        var genres = (await genreTask) ?? new List<string>();
        var likedSongs = (await likedSongsTask) ?? new List<int>();
        var followedArtists = (await followedArtistsTask) ?? new List<ArtistDto>();

        // 3. Aggregate into ViewModel with robust null safety
        return new UserProfileViewModel
        {
            User = user,
            ListeningHistory = history,
            Notifications = notifications,
            Playlists = playlists,
            TopGenres = genres,
            LikedSongsCount = likedSongs.Count(),
            FollowingArtistsCount = followedArtists.Count(),
            FollowedArtists = followedArtists,
            TotalListenTimeMinutes = history.Any() ? history.Count() * 3.5 : 0
        };
    }
}
