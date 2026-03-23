using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("albumartists")]
public class AlbumArtist
{
    [Column("albumid")]
    public int AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [Column("artistid")]        
    public int ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    [Column("role")]
    public string? Role { get; set; }
}
