using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("artists")]
public class Artist
{
    [Key]
    [Column("artistid")]
    public int ArtistId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("bio")]
    public string? Bio { get; set; }

    [Column("country")]
    public string? Country { get; set; }

    [Column("avatarurl")]
    public string? AvatarUrl { get; set; }

    [Column("bannerurl")]
    public string? BannerUrl { get; set; }

    [Column("isverified")]
    public bool IsVerified { get; set; }

    [Column("subscribercount")]
    public int SubscriberCount { get; set; }

    public ICollection<SongArtist> SongArtists { get; set; } = new List<SongArtist>();
    public ICollection<AlbumArtist> AlbumArtists { get; set; } = new List<AlbumArtist>();

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
