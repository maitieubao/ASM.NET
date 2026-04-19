using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Models.ViewModels;

public class SongFormViewModel
{
    public SongDto Song { get; set; } = new();
    public IEnumerable<AlbumDto> Albums { get; set; } = new List<AlbumDto>();
    public IEnumerable<GenreDto> Genres { get; set; } = new List<GenreDto>();
}
