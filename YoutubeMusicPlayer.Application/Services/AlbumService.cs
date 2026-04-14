using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class AlbumService : IAlbumService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IYoutubeService _youtubeService;
    private readonly IDeezerService _deezerService;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundQueue _backgroundQueue;
    private readonly ILogger<AlbumService> _logger;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public AlbumService(
        IUnitOfWork unitOfWork, 
        IYoutubeService youtubeService, 
        IDeezerService deezerService, 
        IMemoryCache cache, 
        IServiceScopeFactory scopeFactory,
        IBackgroundQueue backgroundQueue,
        ILogger<AlbumService> logger)
    {
        _unitOfWork = unitOfWork;
        _youtubeService = youtubeService;
        _deezerService = deezerService;
        _cache = cache;
        _scopeFactory = scopeFactory;
        _backgroundQueue = backgroundQueue;
        _logger = logger;
    }

    public async Task<IEnumerable<AlbumDto>> GetAllAlbumsAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.Repository<Album>().Query()
            .AsNoTracking()
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.ReleaseDate)
            .Select(a => new AlbumDto
            {
                AlbumId = a.AlbumId,
                Title = a.Title,
                AlbumType = a.AlbumType,
                CoverImageUrl = a.CoverImageUrl,
                ReleaseDate = a.ReleaseDate,
                RecordLabel = a.RecordLabel,
                Upc = a.Upc
            })
            .ToListAsync(ct);
    }

    public async Task<(IEnumerable<AlbumDto> Albums, int TotalCount)> GetPaginatedAlbumsAsync(int page, int pageSize, string? searchTerm = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _unitOfWork.Repository<Album>().Query()
            .AsNoTracking()
            .Where(a => !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(a => EF.Functions.Like(a.Title, $"%{searchTerm}%"));
        }

        int totalCount = await query.CountAsync(ct);
        var albums = await query
            .OrderByDescending(a => a.ReleaseDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Artist)
            .ToListAsync(ct);

        return (albums.Select(MapToDtoWithArtists), totalCount);
    }

    public async Task<AlbumDto?> GetAlbumByIdAsync(int id, CancellationToken ct = default)
    {
        var album = await _unitOfWork.Repository<Album>().Query()
            .Include(a => a.Songs)
            .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Artist)
            .Where(a => a.AlbumId == id && !a.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (album == null) return null;

        var artists = album.AlbumArtists.Select(aa => aa.Artist).ToList();
        
        if (!album.Songs.Any() || string.IsNullOrEmpty(album.DeezerAlbumId)) 
        {
             string? firstArtist = artists.FirstOrDefault()?.Name;
             await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp, cancellationToken) => {
                 var syncService = sp.GetRequiredService<IAlbumService>();
                 await syncService.EnsureAlbumSyncMetadataBackground(id, firstArtist, cancellationToken);
             });
        }

        var dto = MapToDto(album);
        dto.Songs = album.Songs.OrderBy(s => s.SongId).Select(s => new SongDto
        {
            SongId = s.SongId,
            Title = s.Title,
            Duration = s.Duration,
            YoutubeVideoId = s.YoutubeVideoId,
            ThumbnailUrl = s.ThumbnailUrl,
            PlayCount = s.PlayCount,
            IsExplicit = s.IsExplicit,
            IsPremiumOnly = s.IsPremiumOnly,
            AuthorName = string.Join(", ", artists.Select(a => a.Name))
        });
        
        dto.Artists = artists.Select(a => new ArtistDto
        {
            ArtistId = a.ArtistId,
            Name = a.Name,
            AvatarUrl = a.AvatarUrl,
            IsVerified = a.IsVerified
        });
        
        dto.CopyrightText = $"© {album.ReleaseDate?.Year ?? DateTime.Now.Year} {album.RecordLabel ?? "YoutubeMusicPlayer Records"}";

        return dto;
    }

    public async Task CreateAlbumAsync(AlbumDto dto, CancellationToken ct = default)
    {
        var album = new Album
        {
            Title = dto.Title,
            AlbumType = dto.AlbumType,
            CoverImageUrl = dto.CoverImageUrl,
            ReleaseDate = dto.ReleaseDate,
            RecordLabel = dto.RecordLabel,
            Upc = dto.Upc
        };
        await _unitOfWork.Repository<Album>().AddAsync(album, ct);
        await _unitOfWork.CompleteAsync(ct);
    }

    public async Task UpdateAlbumAsync(AlbumDto dto, CancellationToken ct = default)
    {
        var album = await _unitOfWork.Repository<Album>().GetByIdAsync(dto.AlbumId, ct);
        if (album != null && !album.IsDeleted)
        {
            album.Title = dto.Title;
            album.AlbumType = dto.AlbumType;
            album.CoverImageUrl = dto.CoverImageUrl;
            album.ReleaseDate = dto.ReleaseDate;
            album.RecordLabel = dto.RecordLabel;
            album.Upc = dto.Upc;
            _unitOfWork.Repository<Album>().Update(album);
            await _unitOfWork.CompleteAsync(ct);
        }
    }

    public async Task DeleteAlbumAsync(int id, CancellationToken ct = default)
    {
        var album = await _unitOfWork.Repository<Album>().GetByIdAsync(id, ct);
        if (album != null && !album.IsDeleted)
        {
            album.IsDeleted = true;
            _unitOfWork.Repository<Album>().Update(album);
            await _unitOfWork.CompleteAsync(ct);
        }
    }

    public async Task<IEnumerable<AlbumDto>> GetRecentAlbumsAsync(int count, CancellationToken ct = default)
    {
        return await _unitOfWork.Repository<Album>().Query()
            .AsNoTracking()
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.ReleaseDate)
            .Take(count)
            .Select(a => new AlbumDto
            {
                AlbumId = a.AlbumId,
                Title = a.Title,
                CoverImageUrl = a.CoverImageUrl,
                ReleaseDate = a.ReleaseDate
            })
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<AlbumDto>> SearchAlbumsAsync(string query, int count, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<AlbumDto>();
        return await _unitOfWork.Repository<Album>().Query()
            .AsNoTracking()
            .Where(a => !a.IsDeleted && EF.Functions.Like(a.Title, $"%{query}%"))
            .Take(count)
            .Select(a => new AlbumDto
            {
                AlbumId = a.AlbumId,
                Title = a.Title,
                CoverImageUrl = a.CoverImageUrl,
                ReleaseDate = a.ReleaseDate
            })
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<AlbumDto>> SearchAlbumsAsync(string query, CancellationToken ct = default)
    {
        return await SearchAlbumsAsync(query, 10, ct);
    }

    public async Task<IEnumerable<AlbumDto>> GetTrendingAlbumsAsync(int count, CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 50);
        string cacheKey = $"trending_albums_v3_{count}";
        
        if (_cache.TryGetValue(cacheKey, out List<AlbumDto>? cachedResult) && cachedResult != null)
            return cachedResult;

        var myLock = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        bool acquired = false;
        try
        {
            acquired = await myLock.WaitAsync(TimeSpan.FromSeconds(15), ct);
            if (!acquired) return await GetFallbackTrending(count, cacheKey, ct);

            // Double-check cache inside lock
            if (_cache.TryGetValue(cacheKey, out cachedResult) && cachedResult != null)
                return cachedResult;

            using var scope = _scopeFactory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var dz = scope.ServiceProvider.GetRequiredService<IDeezerService>();

            var deezerAlbums = await dz.GetNewReleasesAsync(count);
            if (!deezerAlbums.Any()) return await GetFallbackTrending(count, cacheKey, ct);

            var albumTitles = deezerAlbums.Select(a => a.Title.Trim()).Distinct().Take(50).ToList();
            var res = new List<AlbumDto>();

            var existingAlbums = await uow.Repository<Album>().Query()
                .AsNoTracking()
                .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Artist)
                .Where(a => !a.IsDeleted && albumTitles.Contains(a.Title))
                .ToListAsync(ct);

            var albumDict = existingAlbums
                .GroupBy(a => a.Title.Trim())
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var sa in deezerAlbums)
            {
                if (res.Count >= count) break;
                if (albumDict.TryGetValue(sa.Title.Trim(), out var existingAlbum))
                {
                    res.Add(MapToDtoWithArtists(existingAlbum));
                }
            }

            if (res.Any())
            {
                _cache.Set(cacheKey, res, new MemoryCacheEntryOptions {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4),
                    SlidingExpiration = TimeSpan.FromHours(1),
                    Size = 1 
                });
                return res;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing trending albums from Deezer");
        }
        finally
        {
            if (acquired) myLock.Release();
        }

        return await GetFallbackTrending(count, cacheKey, ct);
    }

    private async Task<IEnumerable<AlbumDto>> GetFallbackTrending(int count, string cacheKey, CancellationToken ct)
    {
        var dbAlbums = await _unitOfWork.Repository<Album>().Query()
            .AsNoTracking()
            .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Artist)
            .Where(a => !a.IsDeleted && !string.IsNullOrEmpty(a.CoverImageUrl))
            .OrderByDescending(a => a.ReleaseDate)
            .Take(count)
            .ToListAsync(ct);

        var fallback = dbAlbums.Select(MapToDtoWithArtists).ToList();
        if (fallback.Any())
        {
            _cache.Set(cacheKey, fallback, new MemoryCacheEntryOptions {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                Size = 1
            });
        }
        return fallback;
    }

    private AlbumDto MapToDto(Album a) => new AlbumDto
    {
        AlbumId = a.AlbumId,
        Title = a.Title,
        AlbumType = a.AlbumType,
        CoverImageUrl = a.CoverImageUrl,
        ReleaseDate = a.ReleaseDate,
        RecordLabel = a.RecordLabel,
        Upc = a.Upc
    };

    private AlbumDto MapToDtoWithArtists(Album a)
    {
        var dto = MapToDto(a);
        dto.Artists = a.AlbumArtists?.Select(aa => new ArtistDto {
            ArtistId = aa.ArtistId,
            Name = aa.Artist.Name,
            AvatarUrl = aa.Artist.AvatarUrl,
            IsVerified = aa.Artist.IsVerified
        }) ?? Enumerable.Empty<ArtistDto>();
        return dto;
    }

    public async Task EnsureAlbumSyncMetadataBackground(int albumId, string? artistName, CancellationToken ct)
    {
        try 
        {
             // Note: Using injected services instead of new scope as it is already provided by BackgroundQueue
             var album = await _unitOfWork.Repository<Album>().GetByIdAsync(albumId, ct);
             if (album == null || album.IsDeleted) return;

             if (string.IsNullOrEmpty(album.DeezerAlbumId)) 
             {
                string query = string.IsNullOrWhiteSpace(artistName) ? album.Title : $"{artistName} {album.Title}";
                var searchResults = await _deezerService.SearchAlbumsAsync(query, 1);
                var res = searchResults.FirstOrDefault();
                if (res != null) {
                    album.DeezerAlbumId = res.DeezerId;
                    _unitOfWork.Repository<Album>().Update(album);
                    await _unitOfWork.CompleteAsync(ct);
                }
             }
        } 
        catch (Exception ex) 
        {
            _logger.LogError(ex, "Metadata Sync Error for Album {AlbumId}", albumId);
        }
    }
}
