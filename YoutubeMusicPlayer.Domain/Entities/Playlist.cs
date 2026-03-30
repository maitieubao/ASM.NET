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

    [Column("userid")]
    public int? UserId { get; set; }

    [Column("isfeatured")]
    public bool IsFeatured { get; set; } = false;

    [Column("featuredtype")]
    public string? FeaturedType { get; set; } // "TopHits", "Trending", "NewReleases"

    [Required]
    [Column("title")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("coverimageurl")]
    public string? CoverImageUrl { get; set; }

    [Column("visibility")]
    public string Visibility { get; set; } = "Public"; // Public, Private

    [Column("createdat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public User? User { get; set; }

    public ICollection<PlaylistSong> PlaylistSongs { get; set; } = new List<PlaylistSong>();
}
