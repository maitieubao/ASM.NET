using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("songartists")]
public class SongArtist
{
    [Column("songid")]
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;

    [Column("artistid")]
    public int ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    [Column("role")]
    public string? Role { get; set; }
}
