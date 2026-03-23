using System;

namespace YoutubeMusicPlayer.Application.DTOs;

public class SongDto
{
    public int SongId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? AlbumId { get; set; }
    public int? Duration { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string YoutubeVideoId { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? LyricsText { get; set; }
    public string? LyricsSyncUrl { get; set; }
    public string? Isrc { get; set; }
    public bool IsExplicit { get; set; }
    public long PlayCount { get; set; }
    public bool IsPremiumOnly { get; set; }
    public IEnumerable<int> GenreIds { get; set; } = new List<int>();
    public IEnumerable<string> GenreNames { get; set; } = new List<string>();
}
