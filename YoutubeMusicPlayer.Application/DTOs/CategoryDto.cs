using System;

namespace YoutubeMusicPlayer.Application.DTOs;

public class CategoryDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int SongCount { get; set; }
}
