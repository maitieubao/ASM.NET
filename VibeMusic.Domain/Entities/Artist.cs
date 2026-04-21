using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeMusic.Domain.Entities;

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

    [Column("wikipedia_url")]
    public string? WikipediaUrl { get; set; }

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

    [Column("verification_status")]
    public ArtistVerificationStatus VerificationStatus { get; set; } = ArtistVerificationStatus.Pending;

    [Column("deezer_artist_id")]
    public string? DeezerArtistId { get; set; }

    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }

    public ICollection<SongArtist> SongArtists { get; set; } = new List<SongArtist>();
    public ICollection<AlbumArtist> AlbumArtists { get; set; } = new List<AlbumArtist>();

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
