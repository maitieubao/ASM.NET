using System.Collections.Generic;
using System.Threading.Tasks;
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

    public ProfileFacade(IUserService userService, 
                         INotificationService notificationService, 
                         IPlaylistService playlistService, 
                         IInteractionService interactionService,
                         IArtistService artistService)
    {
        _userService = userService;
        _notificationService = notificationService;
        _playlistService = playlistService;
        _interactionService = interactionService;
        _artistService = artistService;
    }

    public async Task<UserProfileViewModel> BuildUserProfileAsync(int userId)
    {
        // 1. Kick off tasks in parallel (Parallelism)
        var userTask = _userService.GetUserByIdAsync(userId);
        var historyTask = _userService.GetUserListeningHistoryAsync(userId);
        var notifyTask = _notificationService.GetUserNotificationsAsync(userId);
        var playlistTask = _playlistService.GetUserPlaylistsAsync(userId);
        var genreTask = _interactionService.GetTopPreferredGenresAsync(userId);
        var likedSongsTask = _interactionService.GetLikedSongIdsAsync(userId);
        var followedArtistsTask = _artistService.GetFollowedArtistsAsync(userId);

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
