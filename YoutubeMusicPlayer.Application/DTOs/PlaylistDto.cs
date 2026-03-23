using System.Collections.Generic;

namespace YoutubeMusicPlayer.Application.DTOs;

public class PlaylistDto
{
    public int PlaylistId { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public IEnumerable<int> SongIds { get; set; } = new List<int>();
}
