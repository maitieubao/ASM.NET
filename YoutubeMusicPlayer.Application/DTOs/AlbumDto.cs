using System;

namespace YoutubeMusicPlayer.Application.DTOs;

public class AlbumDto
{
    public int AlbumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AlbumType { get; set; }
    public string? CoverImageUrl { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? ReleaseYear => ReleaseDate?.Year.ToString() ?? "N/A";
}
