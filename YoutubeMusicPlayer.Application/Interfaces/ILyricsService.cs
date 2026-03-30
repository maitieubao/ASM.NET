using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface ILyricsService
{
    /// <summary>
    /// Fetches lyrics for a given song and artist.
    /// </summary>
    Task<string?> GetLyricsAsync(string artist, string title);
}
