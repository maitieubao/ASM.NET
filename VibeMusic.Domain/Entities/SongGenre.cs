using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("songgenres")]
public class SongGenre
{
    [Column("songid")]
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;

    [Column("genreid")]
    public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}
