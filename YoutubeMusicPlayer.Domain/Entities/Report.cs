using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("reports")]
public class Report
{
    [Key]
    [Column("reportid")]
    public int ReportId { get; set; }

    [Required]
    [Column("userid")]
    public int UserId { get; set; } // The person reporting

    [Required]
    [Column("targettype")]
    public string TargetType { get; set; } = string.Empty; // "Song", "Playlist", "Comment", "User"

    [Required]
    [Column("targetid")]
    public string TargetId { get; set; } = string.Empty; // ID of the song, user, etc.

    [Required]
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("details")]
    public string? Details { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Pending"; // "Pending", "Resolved", "Dismissed"

    [Column("createdat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("resolvedat")]
    public DateTime? ResolvedAt { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}
