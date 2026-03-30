using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("artist_followers")]
public class ArtistFollower
{
    [Column("userid")]
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    [Column("artistid")]
    public int ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;
    
    [Column("followedat")]
    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
}
