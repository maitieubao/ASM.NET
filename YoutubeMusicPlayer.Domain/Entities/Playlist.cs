using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("playlists")]
public class Playlist
{
    [Key]
    [Column("playlistid")]
    public int PlaylistId { get; set; }

    [Required]
    [Column("userid")]
    public int UserId { get; set; }

    [Required]
    [Column("title")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("coverimageurl")]
    public string? CoverImageUrl { get; set; }

    [Column("createdat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    public ICollection<PlaylistSong> PlaylistSongs { get; set; } = new List<PlaylistSong>();
}
