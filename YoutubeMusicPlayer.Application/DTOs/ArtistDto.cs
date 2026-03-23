using System.Collections.Generic;

namespace YoutubeMusicPlayer.Application.DTOs;

public class ArtistDto
{
    public int ArtistId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? Country { get; set; }
    public string? AvatarUrl { get; set; }
    public string? BannerUrl { get; set; }
    public bool IsVerified { get; set; }
    public int SubscriberCount { get; set; }
    public string? MonthlyListeners { get; set; }
    public IEnumerable<SongDto> Songs { get; set; } = new List<SongDto>();
    public IEnumerable<AlbumDto> Albums { get; set; } = new List<AlbumDto>();

    // Tabs & Pagination
    public IEnumerable<SongDto> TopSongs { get; set; } = new List<SongDto>();
    public IEnumerable<SongDto> LatestSongs { get; set; } = new List<SongDto>();
    public IEnumerable<SongDto> PaginatedSongs { get; set; } = new List<SongDto>();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalSongsCount { get; set; }
}
