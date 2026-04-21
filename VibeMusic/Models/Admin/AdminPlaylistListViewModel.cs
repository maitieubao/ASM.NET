using System.Collections.Generic;
using VibeMusic.Application.DTOs;

namespace VibeMusic.Models.Admin;

public class AdminPlaylistListViewModel
{
    public IEnumerable<PlaylistDto> Playlists { get; set; } = new List<PlaylistDto>();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;
    public string? SearchTerm { get; set; }
}
