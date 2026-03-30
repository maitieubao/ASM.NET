using System;

namespace YoutubeMusicPlayer.Application.DTOs;

public class ListeningHistoryDto
{
    public int HistoryId { get; set; }
    public int UserId { get; set; }
    public int SongId { get; set; }
    public string SongTitle { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public DateTime ListenedAt { get; set; }
}
