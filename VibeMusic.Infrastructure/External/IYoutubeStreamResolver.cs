namespace VibeMusic.Infrastructure.External;

public interface IYoutubeStreamResolver
{
    Task<string> ResolveAsync(string videoId, string? title = null, string? artist = null);
}
