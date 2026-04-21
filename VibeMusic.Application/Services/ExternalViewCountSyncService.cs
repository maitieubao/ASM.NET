using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;

namespace VibeMusic.Application.Services;

public class ExternalViewCountSyncService : IExternalViewCountSyncService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IYoutubeService _youtubeService;
    private readonly IDeezerService _deezerService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExternalViewCountSyncService> _logger;

    public ExternalViewCountSyncService(
        IUnitOfWork unitOfWork,
        IYoutubeService youtubeService,
        IDeezerService deezerService,
        IMemoryCache cache,
        ILogger<ExternalViewCountSyncService> logger)
    {
        _unitOfWork = unitOfWork;
        _youtubeService = youtubeService;
        _deezerService = deezerService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SyncResult> SyncSongAsync(int songId, ViewCountSource source, CancellationToken ct = default)
    {
        var song = await _unitOfWork.Repository<Song>().Query()
            .AsNoTracking()
            .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
            .FirstOrDefaultAsync(s => s.SongId == songId && !s.IsDeleted, ct);

        if (song == null)
            return new SyncResult { Success = false, ErrorMessage = "Song not found", SyncedAt = DateTime.UtcNow };

        long? newCount = null;

        try
        {
            /* 
             * LEGACY SYNC LOGIC: 
             * This service was originally designed to pull massive view metrics from external APIs.
             * DANGER: High YouTube view counts (e.g. 100M) should NOT be synced directly into the 
             * main 'PlayCount' column of the 'Songs' table, as it pollutes internal system stats.
             */
            if (source == ViewCountSource.YouTube)
            {
                if (string.IsNullOrEmpty(song.YoutubeVideoId))
                    return new SyncResult { Success = false, ErrorMessage = "Song has no YoutubeVideoId", SyncedAt = DateTime.UtcNow };

                // Fetching real-time metrics from YouTubeExplode
                var details = await _youtubeService.GetVideoDetailsAsync($"https://youtube.com/watch?v={song.YoutubeVideoId}");
                newCount = details?.ViewCount;
            }
            else if (source == ViewCountSource.Deezer)
            {
                // Deezer uses 'Popularity' which is a relative rank (0-1000 approx)
                var artistName = song.SongArtists.FirstOrDefault()?.Artist?.Name ?? "";
                var track = await _deezerService.SearchTrackAsync(song.Title, artistName);
                newCount = track?.Popularity; 
            }

            if (newCount == null)
                return new SyncResult { Success = false, ErrorMessage = $"No data returned from {source}", SyncedAt = DateTime.UtcNow };

            // Upsert ExternalViewCount
            var existing = await _unitOfWork.Repository<ExternalViewCount>()
                .FirstOrDefaultAsync(e => e.SongId == songId && e.Source == source, ct);

            if (existing != null)
            {
                existing.ViewCount = newCount.Value;
                existing.LastUpdated = DateTime.UtcNow;
                existing.IsActive = true;
                _unitOfWork.Repository<ExternalViewCount>().Update(existing);
            }
            else
            {
                await _unitOfWork.Repository<ExternalViewCount>().AddAsync(new ExternalViewCount
                {
                    SongId = songId,
                    Source = source,
                    ViewCount = newCount.Value,
                    LastUpdated = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                }, ct);
            }

            await _unitOfWork.CompleteAsync(ct);
            _cache.Remove($"viewcount_{songId}");

            return new SyncResult { Success = true, NewViewCount = newCount, SyncedAt = DateTime.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for song {SongId} source {Source}", songId, source);
            return new SyncResult { Success = false, ErrorMessage = ex.Message, SyncedAt = DateTime.UtcNow };
        }
    }

    public async Task<IEnumerable<SyncResult>> SyncAllSourcesAsync(int songId, CancellationToken ct = default)
    {
        var results = new List<SyncResult>();
        foreach (var source in new[] { ViewCountSource.YouTube, ViewCountSource.Deezer })
            results.Add(await SyncSongAsync(songId, source, ct));
        return results;
    }
}
