using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("user_genre_stats")]
public class UserGenreStat
{
    [Key]
    [Column("statid")]
    public int StatId { get; set; }

    [Column("userid")]
    public int UserId { get; set; }

    [Column("genre_name")]
    [StringLength(100)]
    public string GenreName { get; set; } = string.Empty;

    [Column("listen_seconds")]
    public double ListenSeconds { get; set; }
    
    [Column("updatedat")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
