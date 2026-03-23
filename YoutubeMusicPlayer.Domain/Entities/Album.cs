using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("albums")]
public class Album
{
    [Key]
    [Column("albumid")]
    public int AlbumId { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("albumtype")]
    public string? AlbumType { get; set; }

    [Column("coverimageurl")]
    public string? CoverImageUrl { get; set; }

    [Column("releasedate")]
    public DateTime? ReleaseDate { get; set; }

    [Column("recordlabel")]
    public string? RecordLabel { get; set; }

    [Column("copyrighttext")]
    public string? CopyrightText { get; set; }

    [Column("upc")]
    public string? Upc { get; set; }

    [Column("isexplicit")]
    public bool IsExplicit { get; set; }

    public ICollection<Song> Songs { get; set; } = new List<Song>();
    public ICollection<AlbumArtist> AlbumArtists { get; set; } = new List<AlbumArtist>();
}
