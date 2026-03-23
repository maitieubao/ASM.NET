using System;

namespace YoutubeMusicPlayer.Application.DTOs;

public class UserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
