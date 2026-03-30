using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

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

    public ICollection<SongArtist> SongArtists { get; set; } = new List<SongArtist>();
    public ICollection<SongGenre> SongGenres { get; set; } = new List<SongGenre>();
}
