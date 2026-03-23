using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class ArtistService : IArtistService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWikipediaService _wikipediaService;

    public ArtistService(IUnitOfWork unitOfWork, IWikipediaService wikipediaService)
    {
        _unitOfWork = unitOfWork;
        _wikipediaService = wikipediaService;
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

    public async Task<ArtistDto?> GetArtistByIdAsync(int id, int page = 1, int pageSize = 10)
    {
        var a = await _unitOfWork.Repository<Artist>().GetByIdAsync(id);
        if (a == null) return null;

        // 1. Fetch ALL associated Song IDs
        var songIds = _unitOfWork.Repository<SongArtist>()
            .Find(sa => sa.ArtistId == id)
            .Select(sa => sa.SongId)
            .ToList();

        var allSongs = _unitOfWork.Repository<Song>()
            .Find(s => songIds.Contains(s.SongId))
            .ToList();

        // 2. TABS LOGIC
        // Top 10 by play count
        var topSongs = allSongs.OrderByDescending(s => s.PlayCount).Take(10).ToList();
        
        // Latest 10 by ID/Date
        var latestSongs = allSongs.OrderByDescending(s => s.SongId).Take(10).ToList();

        // Paginated Collection
        var paginatedSongs = allSongs.OrderBy(s => s.Title)
                                     .Skip((page - 1) * pageSize)
                                     .Take(pageSize)
                                     .ToList();

        // 3. Fetch Albums
        var albumIds = _unitOfWork.Repository<AlbumArtist>()
            .Find(aa => aa.ArtistId == id)
            .Select(aa => aa.AlbumId)
            .ToList();

        var albums = _unitOfWork.Repository<Album>()
            .Find(al => albumIds.Contains(al.AlbumId))
            .ToList();

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
                ReleaseDate = al.ReleaseDate 
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
        ReleaseDate = s.ReleaseDate
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
}
