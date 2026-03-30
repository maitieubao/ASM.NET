using System.Collections.Generic;

namespace YoutubeMusicPlayer.Application.DTOs;

public class GenreDetailsDto : GenreDto
{
    public IEnumerable<SongDto> Songs { get; set; } = new List<SongDto>();
    public IEnumerable<AlbumDto> Albums { get; set; } = new List<AlbumDto>();
}
