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

    public ProfileFacade(IUserService userService, 
                         INotificationService notificationService, 
                         IPlaylistService playlistService, 
                         IInteractionService interactionService)
    {
        _userService = userService;
        _notificationService = notificationService;
        _playlistService = playlistService;
        _interactionService = interactionService;
    }

    public async Task<UserProfileViewModel> BuildUserProfileAsync(int userId)
    {
        // 1. Kick off tasks in parallel (Parallelism)
        var userTask = _userService.GetUserByIdAsync(userId);
        var historyTask = _userService.GetUserListeningHistoryAsync(userId);
        var notifyTask = _notificationService.GetUserNotificationsAsync(userId);
        var playlistTask = _playlistService.GetUserPlaylistsAsync(userId);
        var genreTask = _interactionService.GetTopPreferredGenresAsync(userId);

        // 2. Wait for all to complete
        await Task.WhenAll(userTask, historyTask, notifyTask, playlistTask, genreTask);

        var user = await userTask;
        if (user == null) return null!;

        // 3. Aggregate into ViewModel
        return new UserProfileViewModel
        {
            User = user,
            ListeningHistory = await historyTask,
            Notifications = await notifyTask,
            Playlists = await playlistTask,
            TopGenres = await genreTask
        };
    }
}
