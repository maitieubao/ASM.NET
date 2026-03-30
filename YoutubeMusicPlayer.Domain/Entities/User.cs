using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("users")]
public class User
{
    [Key]
    [Column("userid")]
    public int UserId { get; set; }

    [Required]
    [Column("username")]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [Column("email")]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Column("passwordhash")]
    public string? PasswordHash { get; set; } // Null if logged in via Google

    [Column("googleid")]
    public string? GoogleId { get; set; }

    [Required]
    [Column("role")]
    [StringLength(50)]
    public string Role { get; set; } = "Customer"; // Customer, Admin, Artist

    [Column("avatarurl")]
    public string? AvatarUrl { get; set; }

    [Column("createdat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("dateofbirth")]
    public DateTime? DateOfBirth { get; set; }

    [Column("ispremium")]
    public bool IsPremium { get; set; } = false;

    [Column("islocked")]
    public bool IsLocked { get; set; } = false;

    [Column("total_listen_seconds")]
    public double TotalListenSeconds { get; set; } = 0;

    public ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
}
