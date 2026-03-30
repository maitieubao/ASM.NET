using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("genres")]
public class Genre
{
    [Key]
    [Column("genreid")]
    public int GenreId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    public ICollection<SongGenre> SongGenres { get; set; } = new List<SongGenre>();
}
