using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Models.Admin;

public class AdminSongListViewModel
{
    public IEnumerable<SongDto> Songs { get; set; } = new List<SongDto>();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;
    public string? SearchTerm { get; set; }
}

public class AdminArtistListViewModel
{
    public IEnumerable<ArtistDto> Artists { get; set; } = new List<ArtistDto>();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;
    public string? SearchTerm { get; set; }
}

public class AdminAlbumListViewModel
{
    public IEnumerable<AlbumDto> Albums { get; set; } = new List<AlbumDto>();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;
    public string? SearchTerm { get; set; }
}
