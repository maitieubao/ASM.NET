using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("comment_likes")]
public class CommentLike
{
    [Key]
    public int LikeId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int CommentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [ForeignKey("CommentId")]
    public Comment Comment { get; set; } = null!;
}
