using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class ArtistService : IArtistService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWikipediaService _wikipediaService;
    private readonly ISpotifyService _spotifyService;

    public ArtistService(IUnitOfWork unitOfWork, IWikipediaService wikipediaService, ISpotifyService spotifyService)
    {
        _unitOfWork = unitOfWork;
        _wikipediaService = wikipediaService;
        _spotifyService = spotifyService;
    }

    public async Task<IEnumerable<ArtistDto>> GetAllArtistsAsync()
    {
        var artists = await _unitOfWork.Repository<Artist>().GetAllAsync();
        return artists.Select(a => new ArtistDto
        {
            ArtistId = a.ArtistId,
            Name = a.Name,
            Bio = a.Bio,
            Country = a.Country,
            AvatarUrl = a.AvatarUrl,
            BannerUrl = a.BannerUrl,
            IsVerified = a.IsVerified,
            SubscriberCount = a.SubscriberCount,
            MonthlyListeners = (a.SubscriberCount * 1.5).ToString("N0")
        });
    }

    public async Task<(IEnumerable<ArtistDto> Artists, int TotalCount)> GetPaginatedArtistsAsync(int page, int pageSize, string? searchTerm = null)
    {
        var query = _unitOfWork.Repository<Artist>().Query();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(a => a.Name.Contains(searchTerm));
        }

        int totalCount = await query.CountAsync();
        var artists = await query.OrderByDescending(a => a.ArtistId)
                                 .Skip((page - 1) * pageSize)
                                 .Take(pageSize)
                                 .ToListAsync();

        var dtos = artists.Select(a => new ArtistDto
        {
            ArtistId = a.ArtistId,
            Name = a.Name,
            AvatarUrl = a.AvatarUrl,
            SubscriberCount = a.SubscriberCount,
            Bio = a.Bio,
            IsVerified = a.IsVerified
        });

        return (dtos, totalCount);
    }

    public async Task<ArtistDto?> GetArtistByIdAsync(int id, int? currentUserId = null, int page = 1, int pageSize = 10)
    {
        var a = await _unitOfWork.Repository<Artist>().GetByIdAsync(id);
        if (a == null) return null;

        var songIds = _unitOfWork.Repository<SongArtist>()
            .Find(sa => sa.ArtistId == id)
            .Select(sa => sa.SongId)
            .ToList();

        var allSongs = _unitOfWork.Repository<Song>()
            .Find(s => songIds.Contains(s.SongId))
            .ToList();

        var topSongs = allSongs.OrderByDescending(s => s.PlayCount).Take(10).ToList();
        var latestSongs = allSongs.OrderByDescending(s => s.SongId).Take(10).ToList();
        var paginatedSongs = allSongs.OrderBy(s => s.Title)
                                     .Skip((page - 1) * pageSize)
                                     .Take(pageSize)
                                     .ToList();

        var albumIds = _unitOfWork.Repository<AlbumArtist>()
            .Find(aa => aa.ArtistId == id)
            .Select(aa => aa.AlbumId)
            .ToList();

        var albums = _unitOfWork.Repository<Album>()
            .Find(al => albumIds.Contains(al.AlbumId))
            .ToList();

        // Related Artists: other artists sharing the same genre
        var myGenres = await _unitOfWork.Repository<SongGenre>().Query()
            .Where(sg => songIds.Contains(sg.SongId))
            .Select(sg => sg.GenreId)
            .ToListAsync();
        
        // AUTO-SYNC DISABLED: Fulfilling user request to only store "listened" tracks.
        /*
        if (string.IsNullOrEmpty(a.Bio) || albums.Count < 2) {
            _ = Task.Run(async () => await SyncWithDeezerAsync(id)); 
        }
        */

        var relatedArtistIds = await _unitOfWork.Repository<SongGenre>()
            .Query()
            .Where(sg => myGenres.Contains(sg.GenreId))
            .Join(_unitOfWork.Repository<SongArtist>().Query(), sg => sg.SongId, sa => sa.SongId, (sg, sa) => sa.ArtistId)
            .Where(aid => aid != id)
            .Distinct()
            .Take(6)
            .ToListAsync();
            
        var relatedArtists = _unitOfWork.Repository<Artist>()
            .Find(ra => relatedArtistIds.Contains(ra.ArtistId))
            .ToList();

        bool isFollowing = false;
        if (currentUserId.HasValue)
        {
            isFollowing = await IsFollowingAsync(currentUserId.Value, id);
        }

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
            MonthlyListeners = (a.SubscriberCount * 1.2 + 50000).ToString("N0"),
            IsFollowing = isFollowing,
            
            TopSongs = topSongs.Select(ToDto),
            LatestSongs = latestSongs.Select(ToDto),
            PaginatedSongs = paginatedSongs.Select(ToDto),
            
            CurrentPage = page,
            TotalSongsCount = allSongs.Count,
            TotalPages = (int)Math.Ceiling(allSongs.Count / (double)pageSize),

            Albums = albums.Select(al => new AlbumDto { 
                AlbumId = al.AlbumId, 
                Title = al.Title, 
                CoverImageUrl = al.CoverImageUrl,
                ReleaseDate = al.ReleaseDate,
                AlbumType = al.AlbumType ?? "Album"
            }),
            
            RelatedArtists = relatedArtists.Select(ra => new ArtistDto {
                ArtistId = ra.ArtistId,
                Name = ra.Name,
                AvatarUrl = ra.AvatarUrl,
                IsVerified = ra.IsVerified
            })
        };
    }

    private SongDto ToDto(Song s) => new SongDto 
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

    public async Task CreateArtistAsync(ArtistDto dto)
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
        await _unitOfWork.Repository<Artist>().AddAsync(artist);
        await _unitOfWork.CompleteAsync();
    }

    public async Task UpdateArtistAsync(ArtistDto dto)
    {
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(dto.ArtistId);
        if (artist != null)
        {
            artist.Name = dto.Name;
            artist.Bio = dto.Bio;
            artist.Country = dto.Country;
            artist.AvatarUrl = dto.AvatarUrl;
            artist.BannerUrl = dto.BannerUrl;
            artist.IsVerified = dto.IsVerified;
            artist.SubscriberCount = dto.SubscriberCount;
            _unitOfWork.Repository<Artist>().Update(artist);
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task DeleteArtistAsync(int id)
    {
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(id);
        if (artist != null)
        {
            var junctions = _unitOfWork.Repository<SongArtist>().Find(sa => sa.ArtistId == id).ToList();
            foreach (var j in junctions) _unitOfWork.Repository<SongArtist>().Remove(j);
            _unitOfWork.Repository<Artist>().Remove(artist);
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task<string?> RefreshArtistBioAsync(int id)
    {
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(id);
        if (artist == null) return null;

        var bio = await _wikipediaService.GetArtistBioAsync(artist.Name);
        if (!string.IsNullOrEmpty(bio))
        {
            artist.Bio = bio;
            _unitOfWork.Repository<Artist>().Update(artist);
            await _unitOfWork.CompleteAsync();
            return bio;
        }
        return null;
    }

    public async Task<IEnumerable<ArtistDto>> GetFollowedArtistsAsync(int userId)
    {
        var followed = await _unitOfWork.Repository<ArtistFollower>().Query()
            .Include(af => af.Artist)
            .Where(af => af.UserId == userId)
            .Select(af => af.Artist)
            .ToListAsync();
            
        return followed.Select(a => new ArtistDto
        {
            ArtistId = a.ArtistId,
            Name = a.Name,
            AvatarUrl = a.AvatarUrl,
            IsVerified = a.IsVerified
        });
    }

    public async Task<bool> IsFollowingAsync(int userId, int artistId)
    {
        return await _unitOfWork.Repository<ArtistFollower>().AnyAsync(af => af.UserId == userId && af.ArtistId == artistId);
    }

    public async Task<bool> ToggleFollowAsync(int userId, int artistId)
    {
        var existing = await _unitOfWork.Repository<ArtistFollower>().FirstOrDefaultAsync(af => af.UserId == userId && af.ArtistId == artistId);
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(artistId);
        
        if (existing != null)
        {
            _unitOfWork.Repository<ArtistFollower>().Remove(existing);
            if (artist != null) artist.SubscriberCount = Math.Max(0, artist.SubscriberCount - 1);
            await _unitOfWork.CompleteAsync();
            return false;
        }
        else
        {
            await _unitOfWork.Repository<ArtistFollower>().AddAsync(new ArtistFollower { UserId = userId, ArtistId = artistId });
            if (artist != null) artist.SubscriberCount++;
            await _unitOfWork.CompleteAsync();
            return true;
        }
    }

    public async Task<string?> SyncWithDeezerAsync(int artistId)
    {
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(artistId);
        if (artist == null) return null;

        Console.WriteLine($"[Artist Sync] Started for: {artist.Name}");

        // 1. Bio Sync (Wikipedia)
        if (string.IsNullOrEmpty(artist.Bio)) {
            artist.Bio = await _wikipediaService.GetArtistBioAsync(artist.Name);
            _unitOfWork.Repository<Artist>().Update(artist);
            await _unitOfWork.CompleteAsync();
        }

        // 2. Discover Metadata (Deezer)
        // Search artist on Deezer first to get their ID if we don't have it
        var tracks = await _spotifyService.SearchTracksAsync(artist.Name, 1);
        var deezerArtistId = tracks?.FirstOrDefault()?.SpotifyArtistId;

        if (!string.IsNullOrEmpty(deezerArtistId)) {
            // Get all albums
            var albums = await _spotifyService.GetArtistAlbumsAsync(deezerArtistId, 20);
            foreach (var da in albums) {
                // Check if exists
                if (!await _unitOfWork.Repository<Album>().AnyAsync(al => al.Title == da.Title)) {
                    var newAlbum = new Album {
                        Title = da.Title,
                        AlbumType = da.AlbumType ?? "Album",
                        CoverImageUrl = da.CoverImageUrl,
                        ReleaseDate = (da.ReleaseDate != null && DateTime.TryParse(da.ReleaseDate, out var dt)) ? dt : DateTime.UtcNow,
                        RecordLabel = "Discovered via Artist Sync"
                    };
                    await _unitOfWork.Repository<Album>().AddAsync(newAlbum);
                    await _unitOfWork.CompleteAsync();

                    await _unitOfWork.Repository<AlbumArtist>().AddAsync(new AlbumArtist { AlbumId = newAlbum.AlbumId, ArtistId = artist.ArtistId });
                    await _unitOfWork.CompleteAsync();
                }
            }
        }

        return artist.Bio;
    }
}
