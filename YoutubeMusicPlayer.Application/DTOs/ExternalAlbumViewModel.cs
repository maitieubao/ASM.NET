using System.Collections.Generic;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.DTOs;

public class ExternalAlbumViewModel
{
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string CoverImageUrl { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // Deezer, iTunes
    public string ExternalId { get; set; } = string.Empty;
    public string? ReleaseDate { get; set; }
    public List<ExternalTrackViewModel> Tracks { get; set; } = new();
}

public class ExternalTrackViewModel
{
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string AlbumName { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public int TrackNumber { get; set; }
}
