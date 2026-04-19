using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("comments")]
public class Comment
{
    [Key]
    [Column("commentid")]
    public int CommentId { get; set; }

    [Required]
    [Column("userid")]
    public int UserId { get; set; }

    [Required]
    [Column("songid")]
    public int SongId { get; set; }

    [Required]
    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("createdat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updatedat")]
    public DateTime? UpdatedAt { get; set; }

    [Column("parentcommentid")]
    public int? ParentCommentId { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [ForeignKey("SongId")]
    public Song Song { get; set; } = null!;
}
