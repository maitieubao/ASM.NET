using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace YoutubeMusicPlayer.Application.Services;

public class AlbumService : IAlbumService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IYoutubeService _youtubeService;
    private readonly ISpotifyService _spotifyService;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public AlbumService(IUnitOfWork unitOfWork, IYoutubeService youtubeService, ISpotifyService spotifyService, IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _unitOfWork = unitOfWork;
        _youtubeService = youtubeService;
        _spotifyService = spotifyService;
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public async Task<IEnumerable<AlbumDto>> GetAllAlbumsAsync()
    {
        var albums = await _unitOfWork.Repository<Album>().Query().OrderByDescending(a => a.ReleaseDate).ToListAsync();
        return albums.Select(a => MapToDto(a));
    }

    public async Task<AlbumDto?> GetAlbumByIdAsync(int id)
    {
        // Optimized: Single query with all relations
        var album = await _unitOfWork.Repository<Album>().Query()
            .Include(a => a.Songs)
            .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Artist)
            .Where(a => a.AlbumId == id)
            .FirstOrDefaultAsync();

        if (album == null) return null;

        var artists = album.AlbumArtists.Select(aa => aa.Artist).ToList();
        
        // AUTO-SYNC DISABLED: Fulfilling user request to only store "listened" tracks.
        /*
        if (!album.Songs.Any()) {
             var deezerSearch = await _spotifyService.SearchTracksAsync($"{album.Title} {artists.FirstOrDefault()?.Name}", 1);
             var deezerAlbumId = deezerSearch.FirstOrDefault()?.SpotifyAlbumId;
             
             if (!string.IsNullOrEmpty(deezerAlbumId)) {
                 // AUTO-SYNC DISABLED: Fulfilling user request to only store "listened" tracks.
             }
        }
        */

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

    public async Task CreateAlbumAsync(AlbumDto dto)
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
        await _unitOfWork.Repository<Album>().AddAsync(album);
        await _unitOfWork.CompleteAsync();
    }

    public async Task UpdateAlbumAsync(AlbumDto dto)
    {
        var album = await _unitOfWork.Repository<Album>().GetByIdAsync(dto.AlbumId);
        if (album != null)
        {
            album.Title = dto.Title;
            album.AlbumType = dto.AlbumType;
            album.CoverImageUrl = dto.CoverImageUrl;
            album.ReleaseDate = dto.ReleaseDate;
            album.RecordLabel = dto.RecordLabel;
            album.Upc = dto.Upc;
            _unitOfWork.Repository<Album>().Update(album);
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task DeleteAlbumAsync(int id)
    {
        var album = await _unitOfWork.Repository<Album>().GetByIdAsync(id);
        if (album != null)
        {
            _unitOfWork.Repository<Album>().Remove(album);
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task<IEnumerable<AlbumDto>> GetRecentAlbumsAsync(int count)
    {
        var albums = await _unitOfWork.Repository<Album>().Query()
            .OrderByDescending(a => a.ReleaseDate)
            .Take(count)
            .ToListAsync();
        return albums.Select(a => MapToDto(a));
    }

    public async Task<IEnumerable<AlbumDto>> SearchAlbumsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<AlbumDto>();
        var albums = await _unitOfWork.Repository<Album>().Query()
            .Where(a => a.Title.ToLower().Contains(query.ToLower()))
            .Take(5)
            .ToListAsync();
        return albums.Select(a => MapToDto(a));
    }

    public async Task<IEnumerable<AlbumDto>> GetTrendingAlbumsAsync(int count)
    {
        string cacheKey = $"trending_albums_{count}";
        if (_cache.TryGetValue(cacheKey, out List<AlbumDto>? cachedResult) && cachedResult != null)
            return cachedResult;

        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var dz = scope.ServiceProvider.GetRequiredService<ISpotifyService>();

        var res = new List<AlbumDto>();
        try
        {
            var deezerAlbums = await dz.GetNewReleasesAsync(count);
            if (!deezerAlbums.Any()) return await GetFallbackTrending(count, cacheKey);

            var albumTitles = deezerAlbums.Select(a => a.Title.ToLower().Trim()).Distinct().ToList();
            var artistNames = deezerAlbums.Select(a => a.ArtistName.ToLower().Trim()).Distinct().ToList();

            var existingAlbums = await uow.Repository<Album>().Query()
                .AsNoTracking()
                .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Artist)
                .Where(a => albumTitles.Contains(a.Title.ToLower()))
                .ToListAsync();

            var existingArtists = await uow.Repository<Artist>().Query()
                .AsNoTracking()
                .Where(a => artistNames.Contains(a.Name.ToLower()))
                .ToListAsync();

            var albumDict = existingAlbums.GroupBy(a => a.Title.ToLower()).ToDictionary(g => g.Key, g => g.First());
            var artistDict = existingArtists.ToDictionary(a => a.Name.ToLower(), a => a);

            var newAlbumsToSave = new List<Album>();
            foreach (var sa in deezerAlbums)
            {
                if (res.Count >= count) break;

                var titleLow = sa.Title.ToLower().Trim();
                var artistLow = sa.ArtistName.ToLower().Trim();

                if (albumDict.TryGetValue(titleLow, out var existingAlbum))
                {
                    res.Add(MapToDtoWithArtists(existingAlbum));
                    continue;
                }

                // New Album
                var newAlbum = new Album {
                    Title = sa.Title,
                    AlbumType = sa.AlbumType ?? "album",
                    CoverImageUrl = sa.CoverImageUrl,
                    ReleaseDate = sa.ReleaseDate != null && DateTime.TryParse(sa.ReleaseDate, out var dt) ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : DateTime.UtcNow,
                    RecordLabel = "Deezer Charts"
                };
                
                if (!artistDict.TryGetValue(artistLow, out var artist))
                {
                    artist = new Artist { Name = sa.ArtistName, AvatarUrl = sa.CoverImageUrl, IsVerified = true };
                    await uow.Repository<Artist>().AddAsync(artist);
                    artistDict[artistLow] = artist;
                }
                
                newAlbum.AlbumArtists.Add(new AlbumArtist { Album = newAlbum, Artist = artist });
                await uow.Repository<Album>().AddAsync(newAlbum);
                newAlbumsToSave.Add(newAlbum);
            }

            if (newAlbumsToSave.Any())
            {
                await uow.CompleteAsync(); 
                foreach (var na in newAlbumsToSave)
                {
                    res.Add(MapToDtoWithArtists(na));
                }
            }

            if (res.Any())
            {
                _cache.Set(cacheKey, res.Take(count).ToList(), TimeSpan.FromHours(2));
                return res.Take(count);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AlbumService] Parallel Trending Error: {ex.Message}");
        }

        return await GetFallbackTrending(count, cacheKey);
    }

    private async Task<IEnumerable<AlbumDto>> GetFallbackTrending(int count, string cacheKey)
    {
        var dbAlbums = await _unitOfWork.Repository<Album>().Query()
            .AsNoTracking()
            .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Artist)
            .Where(a => !string.IsNullOrEmpty(a.CoverImageUrl))
            .OrderByDescending(a => a.ReleaseDate)
            .Take(count)
            .ToListAsync();

        var fallback = dbAlbums.Select(MapToDtoWithArtists).ToList();
        if (fallback.Any()) _cache.Set(cacheKey, fallback, TimeSpan.FromMinutes(15));
        return fallback;
    }

    private async Task ImportSongsFromPlaylist(int albumId, string playlistId)
    {
        try {
            var videos = await _youtubeService.GetPlaylistVideosAsync(playlistId);
            
            // Try to identify the main artist of the playlist
            var artistsInAlbum = new HashSet<int>();
            
            foreach(var v in videos.Take(12)) 
            {
                // Find or create artist
                var artist = await _unitOfWork.Repository<Artist>().Query()
                    .FirstOrDefaultAsync(a => a.Name.ToLower() == v.AuthorName.ToLower());
                
                if (artist == null)
                {
                    artist = new Artist { 
                        Name = v.AuthorName, 
                        AvatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(v.AuthorName)}&background=random",
                        IsVerified = true,
                        SubscriberCount = new Random().Next(10000, 1000000)
                    };
                    await _unitOfWork.Repository<Artist>().AddAsync(artist);
                    await _unitOfWork.CompleteAsync();
                }

                artistsInAlbum.Add(artist.ArtistId);

                if (await _unitOfWork.Repository<Song>().Query().AnyAsync(s => s.YoutubeVideoId == v.YoutubeVideoId)) continue;

                var song = new Song {
                    Title = v.Title,
                    YoutubeVideoId = v.YoutubeVideoId,
                    ThumbnailUrl = v.ThumbnailUrl,
                    Duration = (int?)(v.Duration?.TotalSeconds ?? 0),
                    AlbumId = albumId,
                    PlayCount = (long)new Random().Next(10000, 2000000)
                };
                await _unitOfWork.Repository<Song>().AddAsync(song);
                await _unitOfWork.CompleteAsync();

                // Link song to artist
                if (!await _unitOfWork.Repository<SongArtist>().AnyAsync(sa => sa.SongId == song.SongId && sa.ArtistId == artist.ArtistId))
                {
                    await _unitOfWork.Repository<SongArtist>().AddAsync(new SongArtist { SongId = song.SongId, ArtistId = artist.ArtistId });
                }
            }

            // Link album to artists identified in it
            foreach(var aid in artistsInAlbum)
            {
                if (!await _unitOfWork.Repository<AlbumArtist>().AnyAsync(aa => aa.AlbumId == albumId && aa.ArtistId == aid))
                {
                    await _unitOfWork.Repository<AlbumArtist>().AddAsync(new AlbumArtist { AlbumId = albumId, ArtistId = aid });
                }
            }

            await _unitOfWork.CompleteAsync();
        } catch (Exception ex) {
            Console.WriteLine($"[AlbumService] Album auto-import error: {ex.Message}");
        }
    }

    private AlbumDto MapToDto(Album a)
    {
        return new AlbumDto
        {
            AlbumId = a.AlbumId,
            Title = a.Title,
            AlbumType = a.AlbumType,
            CoverImageUrl = a.CoverImageUrl,
            ReleaseDate = a.ReleaseDate,
            RecordLabel = a.RecordLabel,
            Upc = a.Upc
        };
    }

    private AlbumDto MapToDtoWithArtists(Album a)
    {
        var dto = MapToDto(a);
        if (a.AlbumArtists != null) {
            dto.Artists = a.AlbumArtists.Select(aa => new ArtistDto {
                ArtistId = aa.ArtistId,
                Name = aa.Artist.Name,
                AvatarUrl = aa.Artist.AvatarUrl,
                IsVerified = aa.Artist.IsVerified
            });
        }
        return dto;
    }

    private async Task SyncWithScope(int albumId, string deezerAlbumId, int? artistId = null, string? albumImageUrl = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var yt = scope.ServiceProvider.GetRequiredService<IYoutubeService>();
        var dz = scope.ServiceProvider.GetRequiredService<ISpotifyService>();

        try {
            var tracks = await dz.GetAlbumTracksAsync(deezerAlbumId);
            foreach (var st in tracks) {
                if (await uow.Repository<Song>().Query().AnyAsync(s => s.Title == st.TrackName && s.AlbumId == albumId)) continue;

                // Match with YouTube
                var ytResults = await yt.SearchVideosAsync($"{st.ArtistName} {st.TrackName} official audio", 1);
                var v = ytResults.FirstOrDefault();

                if (v != null) {
                    if (await uow.Repository<Song>().Query().AnyAsync(s => s.YoutubeVideoId == v.YoutubeVideoId)) continue;

                    var song = new Song {
                        Title = st.TrackName,
                        AlbumId = albumId,
                        YoutubeVideoId = v.YoutubeVideoId,
                        ThumbnailUrl = !string.IsNullOrEmpty(st.AlbumImageUrl) ? st.AlbumImageUrl : (albumImageUrl ?? v.ThumbnailUrl),
                        Duration = (int?)(v.Duration?.TotalSeconds ?? (st.DurationMs / 1000)),
                        PlayCount = (int)(v.ViewCount / 1000),
                        ReleaseDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                    };
                    await uow.Repository<Song>().AddAsync(song);
                    await uow.CompleteAsync();

                    if (artistId.HasValue) {
                         await uow.Repository<SongArtist>().AddAsync(new SongArtist { SongId = song.SongId, ArtistId = artistId.Value });
                         await uow.CompleteAsync();
                    }
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"[Deezer Sync] Error syncing tracks for album {albumId}: {ex.Message}");
        }
    }

    private async Task SyncSongsFromDeezerAlbum(int albumId, string deezerAlbumId, int? artistId = null, string? albumImageUrl = null)
    {
        await SyncWithScope(albumId, deezerAlbumId, artistId, albumImageUrl);
    }
}
