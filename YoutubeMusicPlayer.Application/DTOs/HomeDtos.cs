namespace YoutubeMusicPlayer.Application.DTOs;

public class SearchResultDto
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public string Type { get; set; } = "Song"; // Song, Artist, Album
    public string? VideoId { get; set; }
    public int? ArtistId { get; set; }
    public int? AlbumId { get; set; }
    public bool IsVerified { get; set; }
}

public class PlaybackStreamDto
{
    public string? StreamUrl { get; set; }
    public int? SongId { get; set; }
    public bool IsLiked { get; set; }
    public bool ShowAd { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
}

public class RichMetadataDto
{
    public string Lyrics { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
}
