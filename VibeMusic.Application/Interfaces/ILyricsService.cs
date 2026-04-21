using VibeMusic.Application.DTOs;

namespace VibeMusic.Application.Interfaces;

public interface ILyricsService
{
    /// <summary>
    /// Fetches lyrics for a given song and artist.
    /// </summary>
    Task<LyricsResult> GetLyricsAsync(string artist, string title, string? videoId = null);
}
