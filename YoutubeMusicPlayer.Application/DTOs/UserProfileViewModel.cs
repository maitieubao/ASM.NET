using System.Collections.Generic;

namespace YoutubeMusicPlayer.Application.DTOs;

public class UserProfileViewModel
{
    public UserDto User { get; set; } = null!;
    public IEnumerable<ListeningHistoryDto> ListeningHistory { get; set; } = new List<ListeningHistoryDto>();
    public IEnumerable<NotificationDto> Notifications { get; set; } = new List<NotificationDto>();
    public IEnumerable<PlaylistDto> Playlists { get; set; } = new List<PlaylistDto>();
    public IEnumerable<string> TopGenres { get; set; } = new List<string>();
}
