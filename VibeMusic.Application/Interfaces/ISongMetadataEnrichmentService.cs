using VibeMusic.Domain.Entities;

namespace VibeMusic.Application.Interfaces;

/// <summary>
/// Service for enriching song metadata with external data sources (Deezer, etc.)
/// </summary>
public interface ISongMetadataEnrichmentService
{
    /// <summary>
    /// Enrich a single song with metadata from Deezer API
    /// </summary>
    Task<bool> EnrichSongMetadataAsync(Song song, CancellationToken ct = default);
    
    /// <summary>
    /// Enrich multiple songs with metadata from Deezer API
    /// </summary>
    Task<int> EnrichSongsMetadataAsync(IEnumerable<Song> songs, CancellationToken ct = default);
    
    /// <summary>
    /// Enrich all songs of an artist with metadata
    /// </summary>
    Task<int> EnrichArtistSongsMetadataAsync(int artistId, CancellationToken ct = default);
    
    /// <summary>
    /// Check if song metadata needs refresh (older than 7 days)
    /// </summary>
    bool NeedsMetadataRefresh(Song song);
    
    /// <summary>
    /// Refresh metadata for songs that are outdated
    /// </summary>
    Task<int> RefreshOutdatedMetadataAsync(int batchSize = 50, CancellationToken ct = default);
}