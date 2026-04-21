using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;

namespace VibeMusic.Application.Services;

public class SongMetadataEnrichmentService : ISongMetadataEnrichmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDeezerService _deezerService;
    private readonly ILogger<SongMetadataEnrichmentService> _logger;

    public SongMetadataEnrichmentService(
        IUnitOfWork unitOfWork,
        IDeezerService deezerService,
        ILogger<SongMetadataEnrichmentService> logger)
    {
        _unitOfWork = unitOfWork;
        _deezerService = deezerService;
        _logger = logger;
    }

    public async Task<bool> EnrichSongMetadataAsync(Song song, CancellationToken ct = default)
    {
        if (song == null || string.IsNullOrEmpty(song.Title))
        {
            _logger.LogWarning("Cannot enrich song metadata: Song or Title is null/empty");
            return false;
        }

        try
        {
            // Get artist name for better search accuracy
            var artistName = await GetPrimaryArtistNameAsync(song.SongId, ct);
            if (string.IsNullOrEmpty(artistName))
            {
                _logger.LogWarning("No artist found for song {SongId} ({Title})", song.SongId, song.Title);
                return false;
            }

            // Search for track on Deezer
            var deezerTrack = await _deezerService.SearchTrackAsync(song.Title, artistName);
            if (deezerTrack == null)
            {
                _logger.LogInformation("No Deezer match found for song {SongId} ({Title} by {Artist})", 
                    song.SongId, song.Title, artistName);
                return false;
            }

            // Update song with Deezer metadata
            UpdateSongWithDeezerMetadata(song, deezerTrack);
            
            _unitOfWork.Repository<Song>().Update(song);
            await _unitOfWork.CompleteAsync(ct);

            _logger.LogInformation("Successfully enriched song {SongId} ({Title}) with Deezer metadata", 
                song.SongId, song.Title);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching metadata for song {SongId} ({Title})", 
                song.SongId, song.Title);
            return false;
        }
    }

    public async Task<int> EnrichSongsMetadataAsync(IEnumerable<Song> songs, CancellationToken ct = default)
    {
        int enrichedCount = 0;
        var songsList = songs.ToList();

        _logger.LogInformation("Starting metadata enrichment for {Count} songs", songsList.Count);

        foreach (var song in songsList)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                if (await EnrichSongMetadataAsync(song, ct))
                {
                    enrichedCount++;
                }

                // Small delay to avoid overwhelming Deezer API
                await Task.Delay(100, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Metadata enrichment cancelled after {Count} songs", enrichedCount);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch enrichment for song {SongId}", song.SongId);
            }
        }

        _logger.LogInformation("Completed metadata enrichment: {Enriched}/{Total} songs", 
            enrichedCount, songsList.Count);
        return enrichedCount;
    }

    public async Task<int> EnrichArtistSongsMetadataAsync(int artistId, CancellationToken ct = default)
    {
        var songs = await _unitOfWork.Repository<Song>().Query()
            .Where(s => s.SongArtists.Any(sa => sa.ArtistId == artistId) && 
                       !s.IsDeleted && 
                       string.IsNullOrEmpty(s.DeezerTrackId))
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} songs without Deezer metadata for artist {ArtistId}", 
            songs.Count, artistId);

        return await EnrichSongsMetadataAsync(songs, ct);
    }

    public bool NeedsMetadataRefresh(Song song)
    {
        // Needs refresh if:
        // 1. No Deezer metadata at all
        // 2. Metadata is older than 7 days
        return string.IsNullOrEmpty(song.DeezerTrackId) || 
               !song.EnrichedAt.HasValue || 
               song.EnrichedAt.Value < DateTime.UtcNow.AddDays(-7);
    }

    public async Task<int> RefreshOutdatedMetadataAsync(int batchSize = 50, CancellationToken ct = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-7);
        
        var outdatedSongs = await _unitOfWork.Repository<Song>().Query()
            .Where(s => !s.IsDeleted && 
                       (string.IsNullOrEmpty(s.DeezerTrackId) || 
                        !s.EnrichedAt.HasValue || 
                        s.EnrichedAt < cutoffDate))
            .Take(batchSize)
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} songs with outdated metadata", outdatedSongs.Count);

        return await EnrichSongsMetadataAsync(outdatedSongs, ct);
    }

    private async Task<string?> GetPrimaryArtistNameAsync(int songId, CancellationToken ct)
    {
        var artist = await _unitOfWork.Repository<SongArtist>().Query()
            .Include(sa => sa.Artist)
            .Where(sa => sa.SongId == songId && !sa.Artist.IsDeleted)
            .Select(sa => sa.Artist.Name)
            .FirstOrDefaultAsync(ct);

        return artist;
    }

    private void UpdateSongWithDeezerMetadata(Song song, DeezerTrackInfo deezerTrack)
    {
        song.DeezerTrackId = deezerTrack.DeezerTrackId;
        song.DeezerArtistId = deezerTrack.DeezerArtistId;
        song.DeezerAlbumId = deezerTrack.DeezerAlbumId;
        
        // Update ISRC if not already set
        if (string.IsNullOrEmpty(song.Isrc) && !string.IsNullOrEmpty(deezerTrack.ISRC))
        {
            song.Isrc = deezerTrack.ISRC;
        }
        
        // Update duration if not already set (convert from ms to seconds)
        if (!song.Duration.HasValue && deezerTrack.DurationMs > 0)
        {
            song.Duration = deezerTrack.DurationMs / 1000;
        }
        
        // Update explicit flag if Deezer has more accurate info
        if (deezerTrack.IsExplicit && !song.IsExplicit)
        {
            song.IsExplicit = deezerTrack.IsExplicit;
        }

        // Set extended metadata
        song.BPM = deezerTrack.BPM;
        song.PreviewUrl = deezerTrack.PreviewUrl;
        song.TrackNumber = deezerTrack.TrackNumber;
        song.DiskNumber = deezerTrack.DiskNumber;
        song.PopularityRank = deezerTrack.Popularity;
        song.AudioGain = deezerTrack.AudioGain;
        
        // Store available countries as JSON
        if (deezerTrack.AvailableCountries.Any())
        {
            song.AvailableCountries = JsonSerializer.Serialize(deezerTrack.AvailableCountries);
        }
        
        // Update enrichment timestamp
        song.EnrichedAt = DateTime.UtcNow;

        _logger.LogDebug("Updated song {SongId} with Deezer metadata: TrackId={DeezerTrackId}, BPM={BPM}, Popularity={Popularity}", 
            song.SongId, deezerTrack.DeezerTrackId, deezerTrack.BPM, deezerTrack.Popularity);
    }
}