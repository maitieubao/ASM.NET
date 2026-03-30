using System;

namespace YoutubeMusicPlayer.Application.Interfaces;

public class YoutubeAlbumDetails
{
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string YoutubePlaylistId { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? Type { get; set; } // Album, EP, Single
}
