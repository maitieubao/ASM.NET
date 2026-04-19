using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.DTOs;

public class LyricsResult
{
    public string Status { get; set; } = "NOT_FOUND"; // SUCCESS, NOT_FOUND, ERROR
    public string? Lyrics { get; set; }
    public List<TimedLyricLine>? TimedLines { get; set; }
    public string? VideoId { get; set; }
    public string? Language { get; set; }
    public string? ErrorMessage { get; set; }
}
