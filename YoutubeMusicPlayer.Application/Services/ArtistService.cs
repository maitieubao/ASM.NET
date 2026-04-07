using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class ArtistService : IArtistService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWikipediaService _wikipediaService;
    private readonly IDeezerService _deezerService;
    private readonly IYoutubeService _youtubeService;
    private readonly IMemoryCache _cache;
    private readonly IBackgroundQueue _backgroundQueue;
    private readonly ILogger<ArtistService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ArtistService(IUnitOfWork unitOfWork, 
                         IWikipediaService wikipediaService, 
                         IDeezerService deezerService,
                         IYoutubeService youtubeService,
                         IMemoryCache cache,
                         IBackgroundQueue backgroundQueue,
                         ILogger<ArtistService> logger,
                         IServiceScopeFactory scopeFactory)
    {
        _unitOfWork = unitOfWork;
        _wikipediaService = wikipediaService;
        _deezerService = deezerService;
        _youtubeService = youtubeService;
        _cache = cache;
        _backgroundQueue = backgroundQueue;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<IEnumerable<ArtistDto>> GetAllArtistsAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.Repository<Artist>().Query()
            .AsNoTracking()
            .Where(a => !a.IsDeleted)
            .Select(a => MapToDto(a))
            .ToListAsync(ct);
    }

    public async Task<(IEnumerable<ArtistDto> Artists, int TotalCount)> GetPaginatedArtistsAsync(int page, int pageSize, string? searchTerm = null, CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<Artist>().Query().Where(a => !a.IsDeleted);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(a => a.Name.Contains(searchTerm));
        }

        int totalCount = await query.CountAsync(ct);
        var artists = await query.OrderByDescending(a => a.ArtistId)
                                 .Skip((page - 1) * pageSize)
                                 .Take(pageSize)
                                 .ToListAsync(ct);

        return (artists.Select(MapToDto), totalCount);
    }

    public async Task<ArtistDto?> GetArtistByIdAsync(int id, int? currentUserId = null, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        
        string cacheKey = $"artist_details_{id}_p{page}_s{pageSize}";
        if (_cache.TryGetValue(cacheKey, out ArtistDto? cachedResult) && cachedResult != null)
        {
            if (currentUserId.HasValue) cachedResult.IsFollowing = await IsFollowingAsync(currentUserId.Value, id, ct);
            return cachedResult;
        }

        var a = await _unitOfWork.Repository<Artist>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ArtistId == id && !a.IsDeleted, ct);
        
        if (a == null) return null;

        // Optimized Queries - SQL Processing for Songs/Albums
        var songsQuery = _unitOfWork.Repository<Song>().Query()
            .AsNoTracking()
            .Where(s => s.SongArtists.Any(sa => sa.ArtistId == id) && !s.IsDeleted);

        var topSongs = await songsQuery
            .OrderByDescending(s => s.PlayCount)
            .Take(10)
            .ToListAsync(ct);

        var latestSongs = await songsQuery
            .OrderByDescending(s => s.ReleaseDate ?? DateTime.MinValue)
            .ThenByDescending(s => s.SongId)
            .Take(10)
            .ToListAsync(ct);

        var paginatedSongs = await songsQuery
            .OrderBy(s => s.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var totalSongsCount = await songsQuery.CountAsync(ct);

        var albums = await _unitOfWork.Repository<Album>().Query()
            .AsNoTracking()
            .Where(al => al.AlbumArtists.Any(aa => aa.ArtistId == id) && !al.IsDeleted)
            .OrderByDescending(al => al.ReleaseDate ?? DateTime.MinValue)
            .Take(20)
            .ToListAsync(ct);

        // Metadata Sync Optimization - Trigger background if needed
        bool hasSongs = totalSongsCount > 0;
        if (string.IsNullOrEmpty(a.Bio) || string.IsNullOrEmpty(a.AvatarUrl) || !hasSongs) 
        {
             await _backgroundQueue.QueueBackgroundWorkItemAsync(async sp => {
                 var syncService = sp.GetRequiredService<IArtistService>();
                 await syncService.SyncArtistMetadataAsync(id);
             });
        }

        // Optimized Related Artists (JOIN query instead of IN clause)
        var relatedArtists = await (from ra in _unitOfWork.Repository<Artist>().Query().AsNoTracking()
                                   join rsa in _unitOfWork.Repository<SongArtist>().Query().AsNoTracking() on ra.ArtistId equals rsa.ArtistId
                                   join rsg in _unitOfWork.Repository<SongGenre>().Query().AsNoTracking() on rsa.SongId equals rsg.SongId
                                   where !ra.IsDeleted && ra.ArtistId != id &&
                                         _unitOfWork.Repository<SongGenre>().Query().Any(sg => sg.Song.SongArtists.Any(sa => sa.ArtistId == id) && sg.GenreId == rsg.GenreId)
                                   select ra)
                                   .Distinct()
                                   .OrderByDescending(ra => ra.SubscriberCount)
                                   .Take(6)
                                   .ToListAsync(ct);

        bool isFollowing = false;
        if (currentUserId.HasValue) isFollowing = await IsFollowingAsync(currentUserId.Value, id, ct);

        var dto = MapToDto(a);
        dto.IsFollowing = isFollowing;
        dto.TopSongs = topSongs.Select(ToSongDto);
        dto.LatestSongs = latestSongs.Select(ToSongDto);
        dto.PaginatedSongs = paginatedSongs.Select(ToSongDto);
        dto.CurrentPage = page;
        dto.TotalSongsCount = totalSongsCount;
        dto.TotalPages = (int)Math.Ceiling(totalSongsCount / (double)pageSize);
        dto.Albums = albums.Select(al => new AlbumDto { 
            AlbumId = al.AlbumId, 
            Title = al.Title, 
            CoverImageUrl = al.CoverImageUrl,
            ReleaseDate = al.ReleaseDate,
            AlbumType = al.AlbumType ?? "Album"
        });
        dto.RelatedArtists = relatedArtists.Select(MapToDto);
        
        _cache.Set(cacheKey, dto, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1), Size = 1 });
        return dto;
    }

    public async Task CreateArtistAsync(ArtistDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Bio))
        {
            dto.Bio = await _wikipediaService.GetArtistBioAsync(dto.Name);
        }

        var artist = new Artist 
        { 
            Name = dto.Name, 
            Bio = dto.Bio, 
            Country = dto.Country, 
            AvatarUrl = dto.AvatarUrl,
            BannerUrl = dto.BannerUrl,
            IsVerified = dto.IsVerified,
            SubscriberCount = dto.SubscriberCount
        };
        await _unitOfWork.Repository<Artist>().AddAsync(artist, ct);
        await _unitOfWork.CompleteAsync(ct);
    }

    public async Task UpdateArtistAsync(ArtistDto dto, CancellationToken ct = default)
    {
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(dto.ArtistId, ct);
        if (artist != null && !artist.IsDeleted)
        {
            artist.Name = dto.Name;
            artist.Bio = dto.Bio;
            artist.Country = dto.Country;
            artist.AvatarUrl = dto.AvatarUrl;
            artist.BannerUrl = dto.BannerUrl;
            artist.IsVerified = dto.IsVerified;
            artist.SubscriberCount = dto.SubscriberCount;
            _unitOfWork.Repository<Artist>().Update(artist);
            await _unitOfWork.CompleteAsync(ct);
        }
    }

    public async Task DeleteArtistAsync(int id, CancellationToken ct = default)
    {
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(id, ct);
        if (artist != null && !artist.IsDeleted)
        {
            artist.IsDeleted = true;
            _unitOfWork.Repository<Artist>().Update(artist);
            await _unitOfWork.CompleteAsync(ct);
        }
    }

    public async Task<string?> RefreshArtistBioAsync(int id, CancellationToken ct = default)
    {
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(id, ct);
        if (artist == null || artist.IsDeleted) return null;

        var bio = await _wikipediaService.GetArtistBioAsync(artist.Name);
        if (!string.IsNullOrEmpty(bio))
        {
            artist.Bio = bio;
            _unitOfWork.Repository<Artist>().Update(artist);
            await _unitOfWork.CompleteAsync(ct);
            return bio;
        }
        return null;
    }

    public async Task<IEnumerable<ArtistDto>> GetFollowedArtistsAsync(int userId, CancellationToken ct = default)
    {
        var followed = await _unitOfWork.Repository<ArtistFollower>().Query()
            .Include(af => af.Artist)
            .Where(af => af.UserId == userId && !af.Artist.IsDeleted)
            .Select(af => af.Artist)
            .ToListAsync(ct);
            
        return followed.Select(MapToDto);
    }

    public async Task<bool> IsFollowingAsync(int userId, int artistId, CancellationToken ct = default)
    {
        return await _unitOfWork.Repository<ArtistFollower>().AnyAsync(af => af.UserId == userId && af.ArtistId == artistId, ct);
    }

    public async Task<bool> ToggleFollowAsync(int userId, int artistId, CancellationToken ct = default)
    {
        var existing = await _unitOfWork.Repository<ArtistFollower>().FirstOrDefaultAsync(af => af.UserId == userId && af.ArtistId == artistId, ct);
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(artistId, ct);
        
        if (existing != null)
        {
            _unitOfWork.Repository<ArtistFollower>().Remove(existing);
            if (artist != null) artist.SubscriberCount = Math.Max(0, artist.SubscriberCount - 1);
            await _unitOfWork.CompleteAsync(ct);
            _cache.Remove($"artist_details_{artistId}"); // Partial invalidation
            return false;
        }
        else
        {
            await _unitOfWork.Repository<ArtistFollower>().AddAsync(new ArtistFollower { UserId = userId, ArtistId = artistId }, ct);
            if (artist != null) artist.SubscriberCount++;
            await _unitOfWork.CompleteAsync(ct);
            _cache.Remove($"artist_details_{artistId}");
            return true;
        }
    }

    public async Task<string?> SyncArtistMetadataAsync(int artistId, CancellationToken ct = default)
    {
        try 
        {
            using var scope = _scopeFactory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var wik = scope.ServiceProvider.GetRequiredService<IWikipediaService>();
            var dz = scope.ServiceProvider.GetRequiredService<IDeezerService>();
            var yt = scope.ServiceProvider.GetRequiredService<IYoutubeService>();

            var artist = await uow.Repository<Artist>().GetByIdAsync(artistId, ct);
            if (artist == null || artist.IsDeleted) return null;

            bool modified = false;
            if (string.IsNullOrEmpty(artist.Bio)) {
                artist.Bio = await wik.GetArtistBioAsync(artist.Name);
                modified = true;
            }

            if (string.IsNullOrEmpty(artist.AvatarUrl)) {
                var wikiImg = await wik.GetArtistImageAsync(artist.Name);
                if (!string.IsNullOrEmpty(wikiImg)) {
                    artist.AvatarUrl = wikiImg;
                    artist.BannerUrl = wikiImg;
                    modified = true;
                } else {
                    var tracks = await dz.SearchTracksAsync(artist.Name, 1);
                    var dArtistId = tracks?.FirstOrDefault()?.DeezerArtistId;
                    if (!string.IsNullOrEmpty(dArtistId)) {
                        var info = await dz.GetArtistInfoAsync(dArtistId);
                        if (info != null) {
                            artist.AvatarUrl = info.ImageUrl;
                            artist.BannerUrl = info.ImageUrl;
                            modified = true;
                        }
                    }
                }
            }

            if (modified) {
                uow.Repository<Artist>().Update(artist);
                await uow.CompleteAsync(ct);
            }

            var existingSongsCount = await uow.Repository<SongArtist>().Query().CountAsync(sa => sa.ArtistId == artistId, ct);
            if (existingSongsCount == 0) {
                var searchResults = await yt.SearchVideosAsync($"{artist.Name} official audio", 10);
                var songsToAdd = new List<Song>();
                
                foreach (var v in searchResults) {
                    var song = await uow.Repository<Song>().FirstOrDefaultAsync(s => s.YoutubeVideoId == v.YoutubeVideoId && !s.IsDeleted, ct);
                    if (song == null) {
                        song = new Song {
                            Title = v.Title,
                            YoutubeVideoId = v.YoutubeVideoId,
                            ThumbnailUrl = v.ThumbnailUrl,
                            Duration = (int?)v.Duration?.TotalSeconds,
                            PlayCount = v.ViewCount / 1000,
                            ReleaseDate = DateTime.UtcNow
                        };
                        await uow.Repository<Song>().AddAsync(song, ct);
                        songsToAdd.Add(song);
                    }
                }
                
                await uow.CompleteAsync(ct); // Batch save songs

                foreach (var song in songsToAdd) {
                    if (!await uow.Repository<SongArtist>().AnyAsync(sa => sa.SongId == song.SongId && sa.ArtistId == artistId, ct)) {
                        await uow.Repository<SongArtist>().AddAsync(new SongArtist { SongId = song.SongId, ArtistId = artistId }, ct);
                    }
                }
                await uow.CompleteAsync(ct); // Batch save mappings
            }

            return artist.Bio;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata Sync Error for Artist {ArtistId}", artistId);
            return null;
        }
    }

    public async Task<string?> SyncWithDeezerAsync(int artistId, CancellationToken ct = default)
    {
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(artistId, ct);
        if (artist == null || artist.IsDeleted) return null;

        if (string.IsNullOrEmpty(artist.Bio)) {
            artist.Bio = await _wikipediaService.GetArtistBioAsync(artist.Name);
            _unitOfWork.Repository<Artist>().Update(artist);
            await _unitOfWork.CompleteAsync(ct);
        }

        var tracks = await _deezerService.SearchTracksAsync(artist.Name, 1);
        var deezerArtistId = tracks?.FirstOrDefault()?.DeezerArtistId;

        if (!string.IsNullOrEmpty(deezerArtistId)) {
            var albums = await _deezerService.GetArtistAlbumsAsync(deezerArtistId, 20);
            foreach (var da in albums) {
                if (!await _unitOfWork.Repository<Album>().AnyAsync(al => !al.IsDeleted && al.Title == da.Title, ct)) {
                    var newAlbum = new Album {
                        Title = da.Title,
                        AlbumType = "Album",
                        CoverImageUrl = da.CoverImageUrl,
                        ReleaseDate = DateTime.UtcNow,
                        RecordLabel = "Discovered via Artist Sync"
                    };
                    await _unitOfWork.Repository<Album>().AddAsync(newAlbum, ct);
                    await _unitOfWork.CompleteAsync(ct);

                    await _unitOfWork.Repository<AlbumArtist>().AddAsync(new AlbumArtist { AlbumId = newAlbum.AlbumId, ArtistId = artist.ArtistId }, ct);
                    await _unitOfWork.CompleteAsync(ct);
                }
            }
        }

        return artist.Bio;
    }

    public async Task<IEnumerable<ArtistDto>> SearchArtistsAsync(string query, int count, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(query)) return Enumerable.Empty<ArtistDto>();

        var artists = await _unitOfWork.Repository<Artist>().Query()
            .Where(a => !a.IsDeleted && EF.Functions.Like(a.Name, $"%{query}%"))
            .OrderByDescending(a => a.SubscriberCount)
            .Take(count)
            .ToListAsync(ct);

        return artists.Select(MapToDto);
    }

    public async Task<IEnumerable<ArtistDto>> SearchArtistsAsync(string query, CancellationToken ct = default)
    {
        return await SearchArtistsAsync(query, 10, ct);
    }

    public async Task<bool> ToggleVerifiedStatusAsync(int id, CancellationToken ct = default)
    {
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(id, ct);
        if (artist == null || artist.IsDeleted) return false;

        artist.IsVerified = !artist.IsVerified;
        _unitOfWork.Repository<Artist>().Update(artist);
        await _unitOfWork.CompleteAsync(ct);
        
        _cache.Remove($"artist_details_{id}"); // Invalidate cache
        return true;
    }

    private ArtistDto MapToDto(Artist a) 
    {
        long dailyListeners = (a.SubscriberCount / 10) + (a.ArtistId % 100); // More stable mock if real history not available
        return new ArtistDto
        {
            ArtistId = a.ArtistId,
            Name = a.Name,
            Bio = a.Bio,
            Country = a.Country,
            AvatarUrl = a.AvatarUrl,
            BannerUrl = a.BannerUrl,
            IsVerified = a.IsVerified,
            SubscriberCount = a.SubscriberCount,
            MonthlyListeners = (a.SubscriberCount + (dailyListeners * 30)).ToString("N0")
        };
    }

    private SongDto ToSongDto(Song s) => new SongDto 
    { 
        SongId = s.SongId, 
        Title = s.Title, 
        Duration = s.Duration,
        YoutubeVideoId = s.YoutubeVideoId,
        ThumbnailUrl = s.ThumbnailUrl,
        PlayCount = s.PlayCount,
        ReleaseDate = s.ReleaseDate,
        IsExplicit = s.IsExplicit,
        IsPremiumOnly = s.IsPremiumOnly
    };
}
