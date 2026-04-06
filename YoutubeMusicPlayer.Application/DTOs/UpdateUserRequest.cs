using System;
using System.ComponentModel.DataAnnotations;

namespace YoutubeMusicPlayer.Application.DTOs;

public class UpdateUserRequest
{
    [Required]
    public int UserId { get; set; }

    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public DateTime? DateOfBirth { get; set; }
}
