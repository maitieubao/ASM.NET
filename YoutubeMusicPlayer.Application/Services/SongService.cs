using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class SongService : ISongService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IYoutubeService _youtubeService;
    private readonly IWikipediaService _wikipediaService;
    private readonly IDeezerService _deezerService;
    private readonly ILyricsService _lyricsService;
    private readonly IBackgroundQueue _backgroundQueue;

    public SongService(IUnitOfWork unitOfWork, IYoutubeService youtubeService, IWikipediaService wikipediaService, IDeezerService deezerService, ILyricsService lyricsService, IBackgroundQueue backgroundQueue)
    {
        _unitOfWork = unitOfWork;
        _youtubeService = youtubeService;
        _wikipediaService = wikipediaService;
        _deezerService = deezerService;
        _lyricsService = lyricsService;
        _backgroundQueue = backgroundQueue;
    }

    public async Task<IEnumerable<SongDto>> GetAllSongsAsync(CancellationToken ct = default)
    {
        var songs = await _unitOfWork.Repository<Song>().FindAsync(s => !s.IsDeleted, ct);
        return songs.Select(MapToDto);
    }

    public async Task<(IEnumerable<SongDto> Songs, int TotalCount)> GetPaginatedSongsAsync(int page, int pageSize, string? searchTerm = null, CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<Song>().Query().Where(s => !s.IsDeleted);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(s => s.Title.Contains(searchTerm));
        }

        int totalCount = await query.CountAsync(ct);
        var songs = await query.OrderByDescending(s => s.SongId)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
                               .ToListAsync(ct);

        return (songs.Select(MapToDto), totalCount);
    }

    public async Task<SongDto?> GetSongByIdAsync(int id, CancellationToken ct = default)
    {
        var s = await _unitOfWork.Repository<Song>().Query()
            .Include(s => s.SongArtists)
                .ThenInclude(sa => sa.Artist)
            .FirstOrDefaultAsync(s => s.SongId == id && !s.IsDeleted, ct);

        if (s == null) return null;

        var genreIds = await _unitOfWork.Repository<SongGenre>().Query()
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
        var existing = await _unitOfWork.Repository<Song>().FirstOrDefaultAsync(s => s.YoutubeVideoId == youtubeId && !s.IsDeleted, ct);
        if (existing != null) 
        {
            if (string.IsNullOrEmpty(existing.LyricsText))
            {
                await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp) =>
                {
                    await EnrichSongAsync(existing.SongId);
                });
            }
            return await GetSongByIdAsync(existing.SongId, ct);
        }

        return await ImportAndReturnSongAsync($"https://youtube.com/watch?v={youtubeId}", ct);
    }

    public async Task EnrichSongAsync(int songId, CancellationToken ct = default)
    {
        var song = await _unitOfWork.Repository<Song>().Query()
            .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
            .FirstOrDefaultAsync(s => s.SongId == songId && !s.IsDeleted, ct);
        
        if (song == null) return;
        
        var details = await _youtubeService.GetVideoDetailsAsync($"https://youtube.com/watch?v={song.YoutubeVideoId}");
        await PerformEnrichmentAsync(_unitOfWork, _deezerService, _lyricsService, _wikipediaService, songId, details, ct);
    }

    public async Task<SongDto?> ImportAndReturnSongAsync(string videoUrl, CancellationToken ct = default)
    {
        var details = await _youtubeService.GetVideoDetailsAsync(videoUrl);
        if (details == null || string.IsNullOrEmpty(details.YoutubeVideoId)) return null;

        var existingSong = await _unitOfWork.Repository<Song>().Query()
            .FirstOrDefaultAsync(s => s.YoutubeVideoId == details.YoutubeVideoId && !s.IsDeleted, ct);
        if (existingSong != null) return MapToDto(existingSong);

        var song = new Song
        {
            Title = details.CleanedTitle ?? "Bài hát mới",
            YoutubeVideoId = details.YoutubeVideoId,
            ThumbnailUrl = details.ThumbnailUrl,
            Duration = (int?)(details.Duration?.TotalSeconds),
            PlayCount = (int)(details.ViewCount / 10000),
            ReleaseDate = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Song>().AddAsync(song, ct);
        await _unitOfWork.CompleteAsync(ct);

        var artistName = details.CleanedArtist ?? details.AuthorName ?? "Nghệ sĩ";
        var artist = await _unitOfWork.Repository<Artist>().Query().FirstOrDefaultAsync(a => !a.IsDeleted && a.Name.ToLower() == artistName.ToLower(), ct);
        
        if (artist == null)
        {
            artist = new Artist { 
                Name = artistName, 
                AvatarUrl = details.AuthorAvatarUrl ?? "https://ui-avatars.com/api/?name=" + Uri.EscapeDataString(artistName),
                Bio = "Đang cập nhật..."
            };
            await _unitOfWork.Repository<Artist>().AddAsync(artist, ct);
            await _unitOfWork.CompleteAsync(ct);
        }

        await _unitOfWork.Repository<SongArtist>().AddAsync(new SongArtist { SongId = song.SongId, ArtistId = artist.ArtistId, Role = "Main" }, ct);
        await _unitOfWork.CompleteAsync(ct);

        await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp) => { await EnrichSongAsync(song.SongId); });

        return MapToDto(song);
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
        // Optimized: RAM Protection using Projection
        return await _unitOfWork.Repository<Song>().Query()
            .AsNoTracking()
            .Where(s => !s.IsDeleted && !string.IsNullOrEmpty(s.YoutubeVideoId))
            .Select(s => new { s.YoutubeVideoId, s.PlayCount })
            .ToDictionaryAsync(s => s.YoutubeVideoId, s => s.PlayCount, ct);
    }

    public async Task<IEnumerable<SongDto>> GetTrendingSongsAsync(int count = 10, CancellationToken ct = default)
    {
        try {
            var hits = await _youtubeService.GetTrendingMusicAsync(count);
            var hitDtos = hits
                .Where(h => !string.IsNullOrEmpty(h.YoutubeVideoId))
                .Select(h => new SongDto {
                    Title = h.Title,
                    YoutubeVideoId = h.YoutubeVideoId,
                    ThumbnailUrl = h.ThumbnailUrl,
                    AuthorName = h.AuthorName,
                    PlayCount = h.ViewCount / 1000
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
        LyricsText = s.LyricsText,
        ReleaseDate = s.ReleaseDate
    };

    private async Task PerformEnrichmentAsync(IUnitOfWork uow, IDeezerService dz, ILyricsService ls, IWikipediaService ws, int songId, YoutubeVideoDetails details, CancellationToken ct = default)
    {
        var song = await uow.Repository<Song>().Query()
            .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
            .FirstOrDefaultAsync(s => s.SongId == songId, ct);
        if (song == null) return;

        try {
            var dt = await dz.SearchTrackAsync(details.CleanedTitle, details.CleanedArtist);
            if (dt != null) {
                song.Title = dt.TrackName;
                song.IsExplicit = dt.IsExplicit;
                // Update release date if available
                if (DateTime.TryParse(dt.ReleaseDate, out var rd))
                    song.ReleaseDate = DateTime.SpecifyKind(rd, DateTimeKind.Utc);
            }
            // Lyrics enrichment
            string? lyrics = await ls.GetLyricsAsync(details.CleanedArtist, details.CleanedTitle);
            if (!string.IsNullOrEmpty(lyrics)) song.LyricsText = lyrics;

            uow.Repository<Song>().Update(song);
            await uow.CompleteAsync(ct);
        } catch {}
    }
}
