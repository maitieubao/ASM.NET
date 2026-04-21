using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeMusic.Domain.Entities;

[Table("songs")]
public class Song
{
    [Key]
    [Column("songid")]
    public int SongId { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("albumid")]
    public int? AlbumId { get; set; }
    public Album? Album { get; set; }

    [Column("categoryid")]
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    [Column("duration")]
    public int? Duration { get; set; }

    [Column("releasedate")]
    public DateTime? ReleaseDate { get; set; }

    [Column("youtubevideoid")]
    public string YoutubeVideoId { get; set; } = string.Empty;

    [Column("thumbnailurl")]
    public string? ThumbnailUrl { get; set; }

    [Column("lyricstext")]
    public string? LyricsText { get; set; }

    [Column("lyricssyncurl")]
    public string? LyricsSyncUrl { get; set; }

    [Column("isrc")]
    public string? Isrc { get; set; }

    [Column("isexplicit")]
    public bool IsExplicit { get; set; }

    [Column("playcount")]
    public long PlayCount { get; set; }

    [Column("ispremiumonly")]
    public bool IsPremiumOnly { get; set; }

    [Column("deezer_track_id")]
    public string? DeezerTrackId { get; set; }

    [Column("deezer_artist_id")]
    public string? DeezerArtistId { get; set; }

    [Column("deezer_album_id")]
    public string? DeezerAlbumId { get; set; }

    [Column("enriched_at")]
    public DateTime? EnrichedAt { get; set; }

    [Column("bpm")]
    public float? BPM { get; set; }

    [Column("preview_url")]
    public string? PreviewUrl { get; set; }

    [Column("track_number")]
    public int? TrackNumber { get; set; }

    [Column("disk_number")]
    public int? DiskNumber { get; set; }

    [Column("popularity_rank")]
    public int? PopularityRank { get; set; }

    [Column("audio_gain")]
    public float? AudioGain { get; set; }

    [Column("available_countries")]
    public string? AvailableCountries { get; set; }

    [Column("priority_source")]
    public ViewCountSource? PrioritySource { get; set; }

    public ICollection<SongArtist> SongArtists { get; set; } = new List<SongArtist>();
    public ICollection<SongGenre> SongGenres { get; set; } = new List<SongGenre>();
    public ICollection<SongLike> SongLikes { get; set; } = new List<SongLike>();
    public ICollection<ExternalViewCount> ExternalViewCounts { get; set; } = new List<ExternalViewCount>();

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
