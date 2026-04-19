using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("usersessions")]
public class UserSession
{
    [Key]
    [Column("sessionid")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int SessionId { get; set; }

    [Required]
    [Column("userid")]
    public int UserId { get; set; }

    [Required]
    [Column("token")]
    [StringLength(255)]
    public string Token { get; set; } = string.Empty;

    [Column("expiresat")]
    public DateTime ExpiresAt { get; set; }

    [Column("createdat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("isrevoked")]
    public bool IsRevoked { get; set; } = false;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}
