using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VibeMusic.Application.Interfaces;

namespace VibeMusic.Infrastructure.External;

/// <summary>
/// Enriches YoutubeVideoDetails with metadata from Deezer and generates AI tags.
/// Only performs Deezer enrichment when the content is identified as music.
/// </summary>
public class VideoEnrichmentService : IVideoEnrichmentService
{
    private readonly IDeezerService _deezerService;
    private readonly IMusicContentFilter _contentFilter;
    private readonly ITrackMetadataProcessor _metadataProcessor;
    private readonly ILogger<VideoEnrichmentService> _logger;

    public VideoEnrichmentService(
        IDeezerService deezerService,
        IMusicContentFilter contentFilter,
        ITrackMetadataProcessor metadataProcessor,
        ILogger<VideoEnrichmentService> logger)
    {
        _deezerService = deezerService;
        _contentFilter = contentFilter;
        _metadataProcessor = metadataProcessor;
        _logger = logger;
    }

    public async Task<YoutubeVideoDetails> EnrichAsync(YoutubeVideoDetails details)
    {
        if (!_contentFilter.IsMusic(details))
            return details;

        try
        {
            var deezerTrack = await _deezerService.SearchTrackAsync(details.CleanedTitle, details.CleanedArtist);
            if (deezerTrack != null)
            {
                details.CleanedArtist = deezerTrack.ArtistName;
                details.CleanedTitle = deezerTrack.TrackName;

                var genre = deezerTrack.Genres.Count > 0 ? deezerTrack.Genres[0] : string.Empty;
                if (!string.IsNullOrEmpty(genre))
                {
                    details.Genre = NormalizeGenre(genre);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Deezer enrichment failed for {Title}", details.Title);
            // Return original details unchanged — fall through to tag generation
        }

        // Generate AI tags regardless of Deezer enrichment outcome
        details.Tags = _metadataProcessor.GenerateAiTags(details);

        return details;
    }

    public string NormalizeGenre(string genre)
    {
        var g = genre.ToLower();
        if (g.Contains("pop") || g.Contains("v-pop")) return "Nhạc Pop";
        if (g.Contains("remix") || g.Contains("house") || g.Contains("edm")) return "Remix";
        if (g.Contains("ballad")) return "Ballad";
        if (g.Contains("k-pop") || g.Contains("kpop")) return "K-Pop";
        if (g.Contains("classic")) return "Nhạc Classic";
        return "US-UK";
    }
}
