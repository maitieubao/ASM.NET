using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("notifications")]
public class Notification
{
    [Key]
    [Column("notificationid")]
    public int NotificationId { get; set; }

    [Column("userid")]
    public int? UserId { get; set; } // Null for system-wide notification

    [Required]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("type")]
    public string? Type { get; set; } // "System", "StatusChange", "NewMusic"

    [Column("isread")]
    public bool IsRead { get; set; } = false;

    [Column("createdat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public User? User { get; set; }
}
