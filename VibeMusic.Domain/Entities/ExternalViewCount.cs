using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeMusic.Domain.Entities;

[Table("external_view_counts")]
public class ExternalViewCount
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("song_id")]
    public int SongId { get; set; }

    public Song Song { get; set; } = null!;

    [Column("source")]
    public ViewCountSource Source { get; set; }

    [Column("view_count")]
    public long ViewCount { get; set; }

    [Column("last_updated")]
    public DateTime LastUpdated { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
