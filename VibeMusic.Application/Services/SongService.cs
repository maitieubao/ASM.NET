using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;
using Microsoft.Extensions.Logging;


namespace VibeMusic.Application.Services;

public class SongService : ISongService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IYoutubeService _youtubeService;
    private readonly IWikipediaService _wikipediaService;
    private readonly IDeezerService _deezerService;
    private readonly ILyricsService _lyricsService;
    private readonly IBackgroundQueue _backgroundQueue;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Microsoft.Extensions.Logging.ILogger<SongService> _logger;

    // Static dictionary to manage per-video locks across all instances of SongService
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _videoLocks = new();
    // Global write gate for import writes to avoid Npgsql connector races under heavy parallel background imports
    private static readonly SemaphoreSlim _importWriteGate = new(1, 1);


    public SongService(IUnitOfWork unitOfWork, IYoutubeService youtubeService, IWikipediaService wikipediaService, IDeezerService deezerService, ILyricsService lyricsService, IBackgroundQueue backgroundQueue, IMemoryCache cache, IServiceScopeFactory scopeFactory, Microsoft.Extensions.Logging.ILogger<SongService> logger)
    {
        _unitOfWork = unitOfWork;
        _youtubeService = youtubeService;
        _wikipediaService = wikipediaService;
        _deezerService = deezerService;
        _lyricsService = lyricsService;
        _backgroundQueue = backgroundQueue;
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<SongDto>> GetAllSongsAsync(CancellationToken ct = default)
    {
        var songs = await _unitOfWork.Repository<Song>().Query().AsNoTracking().Where(s => !s.IsDeleted).ToListAsync(ct);
        return songs.Select(MapToDto);
    }

    public async Task<(IEnumerable<SongDto> Songs, int TotalCount)> GetPaginatedSongsAsync(int page, int pageSize, string? searchTerm = null, CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<Song>().Query().Where(s => !s.IsDeleted);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.AsNoTracking().Where(s => s.Title.Contains(searchTerm));
        }
        else
        {
            query = query.AsNoTracking();
        }

        int totalCount = await query.CountAsync(ct);
        var songs = await query.OrderByDescending(s => s.SongId)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
                               .ToListAsync(ct);

        System.Console.WriteLine($"[DB-LOAD] Fetched {songs.Count} songs for Admin Dashboard. (Latest data)");
        return (songs.Select(MapToDto), totalCount);
    }

    public async Task<SongDto?> GetSongByIdAsync(int id, CancellationToken ct = default)
    {
        var s = await _unitOfWork.Repository<Song>().Query()
            .AsNoTracking()
            .Include(s => s.SongArtists)
                .ThenInclude(sa => sa.Artist)
            .FirstOrDefaultAsync(s => s.SongId == id && !s.IsDeleted, ct);

        if (s == null) return null;

        var genreIds = await _unitOfWork.Repository<SongGenre>().Query()
            .AsNoTracking()
            .Where(sg => sg.SongId == id)
            .Select(sg => sg.GenreId)
            .ToListAsync(ct);

        var dto = MapToDto(s);
        dto.GenreIds = genreIds;
        dto.AuthorBio = s.SongArtists.FirstOrDefault()?.Artist?.Bio ?? "Thông tin nghệ sĩ đang được cập nhật...";
        
        return dto;
    }

    public async Task<IEnumerable<SongDto>> GetSongsByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var idsList = ids.ToList();
        var songs = await _unitOfWork.Repository<Song>().Query()
            .AsNoTracking()
            .Include(s => s.SongArtists)
                .ThenInclude(sa => sa.Artist)
            .Where(s => idsList.Contains(s.SongId) && !s.IsDeleted)
            .ToListAsync(ct);

        return songs.Select(MapToDto).OrderBy(s => idsList.IndexOf(s.SongId));
    }

    public async Task<(IEnumerable<SongDto> Songs, int TotalCount)> GetSongsByIdsPaginatedAsync(IEnumerable<int> ids, int page, int pageSize, CancellationToken ct = default)
    {
        var idsList = ids.ToList();
        int totalCount = idsList.Count;
        
        var paginatedIds = idsList.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        
        var songs = await _unitOfWork.Repository<Song>().Query()
            .AsNoTracking()
            .Include(s => s.SongArtists)
                .ThenInclude(sa => sa.Artist)
            .Where(s => paginatedIds.Contains(s.SongId) && !s.IsDeleted)
            .ToListAsync(ct);

        // Maintain original ID order within the page
        var orderedSongs = songs.Select(MapToDto).OrderBy(s => paginatedIds.IndexOf(s.SongId));
        return (orderedSongs, totalCount);
    }

    public async Task CreateSongAsync(SongDto dto, CancellationToken ct = default)
    {
        var s = new Song 
        { 
            Title = dto.Title, 
            Duration = dto.Duration, 
            YoutubeVideoId = dto.YoutubeVideoId, 
            ThumbnailUrl = dto.ThumbnailUrl,
            IsExplicit = dto.IsExplicit,
            PlayCount = dto.PlayCount,
            IsPremiumOnly = dto.IsPremiumOnly,
            AlbumId = dto.AlbumId,
            ReleaseDate = DateTime.UtcNow
        };
        await _unitOfWork.Repository<Song>().AddAsync(s, ct);
        await _unitOfWork.CompleteAsync(ct);

        if (dto.GenreIds != null && dto.GenreIds.Any())
        {
            foreach (var gid in dto.GenreIds)
            {
                await _unitOfWork.Repository<SongGenre>().AddAsync(new SongGenre { SongId = s.SongId, GenreId = gid }, ct);
            }
            await _unitOfWork.CompleteAsync(ct);
        }
    }

    public async Task ImportFromYoutubeAsync(string videoUrl, CancellationToken ct = default)
    {
        await ImportAndReturnSongAsync(videoUrl, ct);
    }

    public async Task<SongDto?> GetOrCreateByYoutubeIdAsync(string youtubeId, CancellationToken ct = default)
    {
        var existing = await _unitOfWork.Repository<Song>().Query().AsNoTracking().FirstOrDefaultAsync(s => s.YoutubeVideoId == youtubeId && !s.IsDeleted, ct);
        if (existing != null) 
        {
            if (string.IsNullOrEmpty(existing.LyricsText))
            {
                var targetId = existing.SongId;
                await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp, ct) =>
                {
                    // Use a fresh scope or ensure service availability
                    var scopeSongService = sp.GetRequiredService<ISongService>();
                    await scopeSongService.EnrichSongAsync(targetId, ct);
                });
            }
            return MapToDto(existing); // Return existing directly to avoid another DB Join query
        }

        // Use fast path for playback: Basic import without API-heavy enrichment
        return await ImportAndReturnSongAsync($"https://youtube.com/watch?v={youtubeId}", ct, true);
    }

    public async Task EnrichSongAsync(int songId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting enrichment for Song ID: {SongId}", songId);
        var song = await _unitOfWork.Repository<Song>().Query()
            .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
            .FirstOrDefaultAsync(s => s.SongId == songId && !s.IsDeleted, ct);
        
        if (song == null) {
            _logger.LogWarning("Song ID: {SongId} not found for enrichment", songId);
            return;
        }
        
        try {
            var details = await _youtubeService.GetVideoDetailsAsync($"https://youtube.com/watch?v={song.YoutubeVideoId}");
            await PerformEnrichmentAsync(_unitOfWork, _deezerService, _lyricsService, _wikipediaService, songId, details, ct);
            _logger.LogInformation("Enrichment completed for Song ID: {SongId}", songId);
        } catch (Exception ex) {
            _logger.LogError(ex, "Enrichment failed for Song ID: {SongId}", songId);
        }
    }

    public async Task<SongDto?> ImportAndReturnSongAsync(string videoUrl, CancellationToken ct = default)
    {
        return await ImportAndReturnSongAsync(videoUrl, ct, false);
    }

    public async Task<SongDto?> ImportAndReturnSongAsync(string videoUrl, CancellationToken ct = default, bool fastPath = false)
    {
        // Use basic details if on fast path to avoid slow external API calls
        var details = fastPath 
            ? await _youtubeService.GetBasicVideoDetailsAsync(videoUrl)
            : await _youtubeService.GetVideoDetailsAsync(videoUrl);
            
        if (details == null || string.IsNullOrEmpty(details.YoutubeVideoId)) return null;

        // Per-video lock to prevent concurrent imports of the same video
        var videoLock = _videoLocks.GetOrAdd(details.YoutubeVideoId, _ => new SemaphoreSlim(1, 1));
        await videoLock.WaitAsync(CancellationToken.None); // Không dùng ct — import phải hoàn thành dù request cancel
        try
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    await _importWriteGate.WaitAsync(CancellationToken.None);
                    Song? song;
                    try
                    {
                        // Re-check after acquiring global gate (another worker may have imported meanwhile)
                        var existingSong = await uow.Repository<Song>().Query()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.YoutubeVideoId == details.YoutubeVideoId && !s.IsDeleted, CancellationToken.None);
                        if (existingSong != null) return MapToDto(existingSong);

                        try
                        {
                            song = new Song
                            {
                                Title = details.CleanedTitle ?? "Bài hát mới",
                                YoutubeVideoId = details.YoutubeVideoId,
                                ThumbnailUrl = details.ThumbnailUrl,
                                Duration = (int?)(details.Duration?.TotalSeconds),
                                PlayCount = 0,
                                ReleaseDate = DateTime.UtcNow
                            };
                            await uow.Repository<Song>().AddAsync(song, CancellationToken.None);

                            var artistName = details.CleanedArtist ?? details.AuthorName ?? "Nghệ sĩ";
                            var artist = await uow.Repository<Artist>().Query()
                                .FirstOrDefaultAsync(a => !a.IsDeleted && a.Name.ToLower() == artistName.ToLower(), CancellationToken.None);

                            if (artist == null)
                            {
                                artist = new Artist
                                {
                                    Name = artistName,
                                    AvatarUrl = details.AuthorAvatarUrl ?? "https://ui-avatars.com/api/?name=" + Uri.EscapeDataString(artistName),
                                    Bio = "Đang cập nhật..."
                                };
                                await uow.Repository<Artist>().AddAsync(artist, CancellationToken.None);
                            }

                            // Save song + artist + relation in one SaveChanges to reduce connector reset/cancellation races.
                            await uow.Repository<SongArtist>().AddAsync(
                                new SongArtist { Song = song, Artist = artist, Role = "Main" },
                                CancellationToken.None);

                            await uow.CompleteAsync(CancellationToken.None);
                        }
                        catch
                        {
                            throw;
                        }
                    }
                    finally
                    {
                        _importWriteGate.Release();
                    }

                    if (song == null) return null;
                    var targetSongId = song.SongId;
                    await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp, _) => {
                        var scopeSongService = sp.GetRequiredService<ISongService>();
                        await scopeSongService.EnrichSongAsync(targetSongId, CancellationToken.None);
                    });

                    return MapToDto(song);
                }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SongService] DB save failed for {VideoId}, checking if already exists",
                    details.YoutubeVideoId);
                // Last resort: check if another thread succeeded
                using var fallbackScope = _scopeFactory.CreateScope();
                var fallbackUow = fallbackScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var fallback = await fallbackUow.Repository<Song>().Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.YoutubeVideoId == details.YoutubeVideoId && !s.IsDeleted, CancellationToken.None);
                if (fallback != null) return MapToDto(fallback);
                return null;
            }


        }
        finally
        {
            videoLock.Release();
            if (videoLock.CurrentCount == 1)
                _videoLocks.TryRemove(details.YoutubeVideoId, out _);
        }
    }

    public async Task UpdateSongAsync(SongDto dto, CancellationToken ct = default)
    {
        var s = await _unitOfWork.Repository<Song>().Query()
            .FirstOrDefaultAsync(s => s.SongId == dto.SongId && !s.IsDeleted, ct);
        if (s != null)
        {
            s.Title = dto.Title;
            s.AlbumId = dto.AlbumId;
            s.YoutubeVideoId = dto.YoutubeVideoId;
            s.ThumbnailUrl = dto.ThumbnailUrl;
            s.IsExplicit = dto.IsExplicit;
            s.PlayCount = dto.PlayCount;
            s.IsPremiumOnly = dto.IsPremiumOnly;
            
            _unitOfWork.Repository<Song>().Update(s);
            
            // Optimized: Genre Diffing - only update what changed
            var existing = await _unitOfWork.Repository<SongGenre>().Query()
                .Where(sg => sg.SongId == s.SongId)
                .ToListAsync(ct);
            
            var existingIds = existing.Select(e => e.GenreId).ToHashSet();
            var newIds = (dto.GenreIds ?? new List<int>()).ToHashSet();

            foreach (var eg in existing.Where(eg => !newIds.Contains(eg.GenreId)))
                _unitOfWork.Repository<SongGenre>().Remove(eg);

            foreach (var nid in newIds.Where(nid => !existingIds.Contains(nid)))
                await _unitOfWork.Repository<SongGenre>().AddAsync(new SongGenre { SongId = s.SongId, GenreId = nid }, ct);

            await _unitOfWork.CompleteAsync(ct);
        }
    }

    public async Task DeleteSongAsync(int id, CancellationToken ct = default)
    {
        var s = await _unitOfWork.Repository<Song>().GetByIdAsync(id, ct);
        if (s != null && !s.IsDeleted)
        {
            s.IsDeleted = true;
            _unitOfWork.Repository<Song>().Update(s);
            await _unitOfWork.CompleteAsync(ct);
        }
    }

    public async Task<Dictionary<string, long>> GetUniversalPlayCountsAsync(CancellationToken ct = default)
    {
        // FIX PERF: Cache play counts để tránh load toàn bộ songs table mỗi request
        const string cacheKey = "universal_play_counts";
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, long>? cachedCounts) && cachedCounts != null)
            return cachedCounts;

        var counts = await _unitOfWork.Repository<Song>().Query()
            .AsNoTracking()
            .Where(s => !s.IsDeleted && !string.IsNullOrEmpty(s.YoutubeVideoId))
            .Select(s => new { s.YoutubeVideoId, s.PlayCount })
            .ToDictionaryAsync(s => s.YoutubeVideoId, s => s.PlayCount, ct);

        _cache.Set(cacheKey, counts, TimeSpan.FromMinutes(30));
        return counts;
    }

    public async Task<IEnumerable<SongDto>> GetTrendingSongsAsync(int count = 10, CancellationToken ct = default)
    {
        try {
            var hits = await _youtubeService.GetTrendingMusicAsync(count);
            
            // Lấy PlayCount thực từ DB cho các bài đã có trong DB
            var youtubeIds = hits.Where(h => !string.IsNullOrEmpty(h.YoutubeVideoId))
                                 .Select(h => h.YoutubeVideoId).ToList();
            
            var dbPlayCounts = await _unitOfWork.Repository<Song>().Query()
                .AsNoTracking()
                .Where(s => !s.IsDeleted && youtubeIds.Contains(s.YoutubeVideoId))
                .Select(s => new { s.YoutubeVideoId, s.PlayCount })
                .ToDictionaryAsync(s => s.YoutubeVideoId, s => s.PlayCount, ct);

            var hitDtos = hits
                .Where(h => !string.IsNullOrEmpty(h.YoutubeVideoId))
                .Select(h => new SongDto {
                    Title = h.Title,
                    YoutubeVideoId = h.YoutubeVideoId,
                    ThumbnailUrl = h.ThumbnailUrl,
                    AuthorName = h.AuthorName,
                    // FIX: Dùng PlayCount thực từ DB nếu có, không dùng ViewCount ảo từ YouTube
                    PlayCount = dbPlayCounts.TryGetValue(h.YoutubeVideoId, out var pc) ? pc : 0
                }).ToList();

            var dbSongs = await _unitOfWork.Repository<Song>().Query()
                .Where(s => !s.IsDeleted && !string.IsNullOrEmpty(s.YoutubeVideoId))
                .OrderByDescending(s => s.PlayCount)
                .Take(count)
                .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
                .ToListAsync(ct);
            
            var dbDtos = dbSongs.Select(MapToDto);

            return hitDtos.Concat(dbDtos.Where(d => !hitDtos.Any(h => h.YoutubeVideoId == d.YoutubeVideoId)))
                          .Take(count);
        } catch {
             var songs = await _unitOfWork.Repository<Song>().Query()
                 .Where(s => !s.IsDeleted && !string.IsNullOrEmpty(s.YoutubeVideoId))
                 .OrderByDescending(s => s.PlayCount)
                 .Take(count)
                 .ToListAsync(ct);
             return songs.Select(MapToDto);
        }
    }

    private SongDto MapToDto(Song s) => new SongDto
    {
        SongId = s.SongId,
        Title = s.Title,
        Duration = s.Duration,
        YoutubeVideoId = s.YoutubeVideoId,
        ThumbnailUrl = s.ThumbnailUrl,
        IsExplicit = s.IsExplicit,
        PlayCount = s.PlayCount,
        IsPremiumOnly = s.IsPremiumOnly,
        AlbumId = s.AlbumId,
        AuthorName = s.SongArtists?.FirstOrDefault()?.Artist?.Name ?? "Nghệ sĩ",
        GenreNames = s.SongGenres?.Select(sg => sg.Genre?.Name).Where(n => n != null).Cast<string>().ToList() ?? new List<string>(),
        LyricsText = s.LyricsText,
        ReleaseDate = s.ReleaseDate
    };

    private async Task PerformEnrichmentAsync(IUnitOfWork uow, IDeezerService dz, ILyricsService ls, IWikipediaService ws, int songId, YoutubeVideoDetails details, CancellationToken ct = default)
    {
        var song = await uow.Repository<Song>().Query()
            .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
            .Include(s => s.SongGenres).ThenInclude(sg => sg.Genre)
            .FirstOrDefaultAsync(s => s.SongId == songId, ct);
        if (song == null) return;

        _logger.LogInformation("[SongService] Starting robust enrichment for song {SongId} ({VideoId})", songId, details.YoutubeVideoId);

        // 1. Deezer Metadata Enrichment
        try {
            var dt = await dz.SearchTrackAsync(details.CleanedTitle, details.CleanedArtist);
            if (dt != null) {
                song.Title = dt.TrackName;
                song.IsExplicit = dt.IsExplicit;
                if (DateTime.TryParse(dt.ReleaseDate, out var rd))
                {
                    song.ReleaseDate = DateTime.SpecifyKind(rd, DateTimeKind.Utc);
                }
                
                // Fetch Genres from Deezer Artist
                if (!string.IsNullOrEmpty(dt.DeezerArtistId))
                {
                    var artistInfo = await dz.GetArtistInfoAsync(dt.DeezerArtistId);
                    if (artistInfo != null && artistInfo.Genres != null && artistInfo.Genres.Any())
                    {
                        var genreRepo = uow.Repository<Genre>();
                        var songGenreRepo = uow.Repository<SongGenre>();
                        
                        foreach (var genreName in artistInfo.Genres)
                        {
                            var genre = await genreRepo.Query()
                                .FirstOrDefaultAsync(g => g.Name.ToLower() == genreName.ToLower(), ct);
                            
                            if (genre == null)
                            {
                                genre = new Genre { Name = genreName };
                                await genreRepo.AddAsync(genre, ct);
                                await uow.CompleteAsync(ct); // Need ID for next step
                            }
                            
                            if (!song.SongGenres.Any(sg => sg.GenreId == genre.GenreId))
                            {
                                await songGenreRepo.AddAsync(new SongGenre { SongId = song.SongId, GenreId = genre.GenreId }, ct);
                            }
                        }
                    }
                }
                
                _logger.LogInformation("[SongService] Updated metadata and genres from Deezer for {SongId}", songId);
            } else {
                _logger.LogInformation("[SongService] No Deezer match for {SongId}", songId);
            }
        } catch (Exception ex) {
            _logger.LogWarning("[SongService] Deezer enrichment failed for {SongId}: {Message}", songId, ex.Message);
        }

        // 2. Lyrics Enrichment (Crucial step but shouldn't block)
        try {
            var lyricsResult = await ls.GetLyricsAsync(details.CleanedArtist, details.CleanedTitle, details.YoutubeVideoId);
            if (lyricsResult.Status == "SUCCESS" && !string.IsNullOrEmpty(lyricsResult.Lyrics)) {
                song.LyricsText = lyricsResult.Lyrics;
                _logger.LogInformation("[SongService] Updated lyrics for {SongId}", songId);
            } else {
                _logger.LogInformation("[SongService] No lyrics found for {SongId} (Status: {Status})", songId, lyricsResult.Status);
            }
        } catch (Exception ex) {
            _logger.LogWarning("[SongService] Lyrics enrichment failed for {SongId}: {Message}", songId, ex.Message);
        }

        // Final Save for all successful updates
        try {
            uow.Repository<Song>().Update(song);
            await uow.CompleteAsync(ct);
            _logger.LogInformation("[SongService] Completed enrichment save for song {SongId}", songId);
        } catch (Exception ex) {
            _logger.LogError(ex, "[SongService] Database update failed during enrichment for {SongId}", songId);
        }
    }

    public async Task<bool> TogglePremiumStatusAsync(int id, CancellationToken ct = default)
    {
        var song = await _unitOfWork.Repository<Song>().GetByIdAsync(id, ct);
        if (song == null) return false;

        song.IsPremiumOnly = !song.IsPremiumOnly;
        _unitOfWork.Repository<Song>().Update(song);
        await _unitOfWork.CompleteAsync(ct);
        return true;
    }

    public async Task<bool> ToggleExplicitStatusAsync(int id, CancellationToken ct = default)
    {
        var song = await _unitOfWork.Repository<Song>().GetByIdAsync(id, ct);
        if (song == null) return false;

        song.IsExplicit = !song.IsExplicit;
        _unitOfWork.Repository<Song>().Update(song);
        await _unitOfWork.CompleteAsync(ct);
        return true;
    }
    public async Task<(string? Lyrics, string? Bio)> GetLyricsAndBioAsync(string videoId, CancellationToken ct = default)
    {
        var data = await _unitOfWork.Repository<Song>().Query()
            .AsNoTracking()
            .Where(s => s.YoutubeVideoId == videoId && !s.IsDeleted)
            .Select(s => new {
                s.SongId,
                s.LyricsText,
                Bio = s.SongArtists.OrderBy(sa => sa.ArtistId).Select(sa => sa.Artist.Bio).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (data == null) return (null, null);

        // Background enrichment if data is stale or missing
        if (string.IsNullOrEmpty(data.LyricsText) || 
            (data.Bio != null && (data.Bio.Contains("automatically imported") || data.Bio.Contains("đang được cập nhật"))))
        {
            var targetId = data.SongId;
            await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp, ct) =>
            {
                var scopeSongService = sp.GetRequiredService<ISongService>();
                await scopeSongService.EnrichSongAsync(targetId, ct);
            });
        }

        return (data.LyricsText, data.Bio);
    }
}
