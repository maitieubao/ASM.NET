namespace VibeMusic.Application.Interfaces;

public interface IVideoEnrichmentService
{
    Task<YoutubeVideoDetails> EnrichAsync(YoutubeVideoDetails details);
    string NormalizeGenre(string genre);
}
