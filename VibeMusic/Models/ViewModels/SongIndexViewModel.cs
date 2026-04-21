using System.Collections.Generic;
using VibeMusic.Application.DTOs;

namespace VibeMusic.Models.ViewModels;

public class SongIndexViewModel
{
    public IEnumerable<SongDto> Songs { get; set; } = new List<SongDto>();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public string? SearchTerm { get; set; }
}
