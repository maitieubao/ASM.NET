using System.Collections.Generic;

namespace YoutubeMusicPlayer.Application.DTOs;

public class PlaylistDto
{
    public int PlaylistId { get; set; }
    public int? UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsFeatured { get; set; }
    public string? FeaturedType { get; set; }
    public string Visibility { get; set; } = "Public";
    public bool IsPublic => Visibility == "Public";
    public int SongsCount => SongIds?.Count() ?? 0;
    public int TotalDurationSeconds { get; set; }
    public IEnumerable<int> SongIds { get; set; } = new List<int>();
    public IEnumerable<SongDto> Songs { get; set; } = new List<SongDto>();
}
