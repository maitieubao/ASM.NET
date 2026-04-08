using System.Collections.Generic;

namespace YoutubeMusicPlayer.Application.DTOs;

public class UserProfileViewModel
{
    public UserDto User { get; set; } = null!;
    public IEnumerable<ListeningHistoryDto> ListeningHistory { get; set; } = new List<ListeningHistoryDto>();
    public IEnumerable<NotificationDto> Notifications { get; set; } = new List<NotificationDto>();
    public IEnumerable<PlaylistDto> Playlists { get; set; } = new List<PlaylistDto>();
    public IEnumerable<string> TopGenres { get; set; } = new List<string>();
    
    // Additional Stats for Optimization
    public int LikedSongsCount { get; set; }
    public int FollowingArtistsCount { get; set; }
    public double TotalListenTimeMinutes { get; set; }
    public IEnumerable<ArtistDto> FollowedArtists { get; set; } = new List<ArtistDto>();
}
