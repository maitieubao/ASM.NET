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
    public string? RecordLabel { get; set; }
    public string? Upc { get; set; }
    public bool IsExplicit { get; set; }
    public string? CopyrightText { get; set; }
    public string? AuthorName => Artists?.FirstOrDefault()?.Name;
    public IEnumerable<SongDto> Songs { get; set; } = new List<SongDto>();
    public IEnumerable<ArtistDto> Artists { get; set; } = new List<ArtistDto>();
}
