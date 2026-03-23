using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("playlistsongs")]
public class PlaylistSong
{
    [Required]
    [Column("playlistid")]
    public int PlaylistId { get; set; }

    [Required]
    [Column("songid")]
    public int SongId { get; set; }

    [Column("addedat")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("PlaylistId")]
    public Playlist Playlist { get; set; } = null!;

    [ForeignKey("SongId")]
    public Song Song { get; set; } = null!;
}
