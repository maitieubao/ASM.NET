using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YoutubeMusicPlayer.Application.DTOs;

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
}
