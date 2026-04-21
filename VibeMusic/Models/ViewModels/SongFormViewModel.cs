using System.Collections.Generic;
using VibeMusic.Application.DTOs;

namespace VibeMusic.Models.ViewModels;

public class SongFormViewModel
{
    public SongDto Song { get; set; } = new();
    public IEnumerable<AlbumDto> Albums { get; set; } = new List<AlbumDto>();
    public IEnumerable<GenreDto> Genres { get; set; } = new List<GenreDto>();
}
