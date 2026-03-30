using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("songlikes")]
public class SongLike
{
    [Column("userid")]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Column("songid")]
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;

    [Column("likedat")]
    public DateTime LikedAt { get; set; } = DateTime.UtcNow;
}
