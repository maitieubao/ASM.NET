using System;

namespace YoutubeMusicPlayer.Application.DTOs;

public class NotificationDto
{
    public int NotificationId { get; set; }
    public int? UserId { get; set; } // Null for system-wide
    public string? UserName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
