using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("listeninghistory")]
public class ListeningHistory
{
    [Key]
    [Column("historyid")]
    public int HistoryId { get; set; }

    [Required]
    [Column("userid")]
    public int UserId { get; set; }

    [Required]
    [Column("songid")]
    public int SongId { get; set; }

    [Column("listenedat")]
    public DateTime ListenedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [ForeignKey("SongId")]
    public Song Song { get; set; } = null!;
}
