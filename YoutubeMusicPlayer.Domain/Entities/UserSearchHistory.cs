using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("usersearchhistory")]
public class UserSearchHistory
{
    [Key]
    [Column("searchid")]
    public int SearchId { get; set; }

    [Required]
    [Column("userid")]
    public int UserId { get; set; }

    [Required]
    [Column("searchquery")]
    [StringLength(255)]
    public string SearchQuery { get; set; } = string.Empty;

    [Column("searchedat")]
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}
