using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VibeMusic.Application.DTOs;

public class SongDto
{
    public int SongId { get; set; }

    [Required(ErrorMessage = "Tiêu đề bài hát không được để trống")]
    [StringLength(255, ErrorMessage = "Tiêu đề không được quá 255 ký tự")]
    public string Title { get; set; } = string.Empty;

    public int? AlbumId { get; set; }
    public int? Duration { get; set; }
    public DateTime? ReleaseDate { get; set; }

    [Required(ErrorMessage = "Youtube Video ID là bắt buộc")]
    [StringLength(50, ErrorMessage = "Youtube Video ID không hợp lệ")]
    public string YoutubeVideoId { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }
    public string? LyricsText { get; set; }
    public string? LyricsSyncUrl { get; set; }
    public string? Isrc { get; set; }
    public bool IsExplicit { get; set; }
    public long PlayCount { get; set; }
    public bool IsPremiumOnly { get; set; }
    public IEnumerable<int> GenreIds { get; set; } = new List<int>();
    public IEnumerable<string> GenreNames { get; set; } = new List<string>();
    public bool IsLiked { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorBio { get; set; }
    
    // Deezer metadata properties
    public string? DeezerTrackId { get; set; }
    public string? DeezerArtistId { get; set; }
    public string? DeezerAlbumId { get; set; }
    public DateTime? EnrichedAt { get; set; }
    public float? BPM { get; set; }
    public string? PreviewUrl { get; set; }
    public int? TrackNumber { get; set; }
    public int? DiskNumber { get; set; }
    public int? PopularityRank { get; set; }
    public float? AudioGain { get; set; }
    public List<string> AvailableCountries { get; set; } = new List<string>();
    public string? AlbumName { get; set; }
    public string? AlbumImageUrl { get; set; }
    
    // Computed properties
    public string FormattedDuration => Duration.HasValue ? TimeSpan.FromSeconds(Duration.Value).ToString(@"mm\:ss") : "N/A";
    public string FormattedPlayCount => PlayCount >= 1_000_000 ? $"{PlayCount / 1_000_000.0:F1}M" : PlayCount >= 1_000 ? $"{PlayCount / 1_000.0:F1}K" : PlayCount.ToString();
    public string FormattedBPM => BPM.HasValue ? $"{BPM.Value:F0} BPM" : "N/A";
    public string FormattedPopularity => PopularityRank.HasValue && PopularityRank.Value > 0 ? $"#{PopularityRank.Value}" : "N/A";
    public bool HasDeezerMetadata => !string.IsNullOrEmpty(DeezerTrackId);
    public bool IsMetadataFresh => EnrichedAt.HasValue && EnrichedAt.Value > DateTime.UtcNow.AddDays(-7);
}
