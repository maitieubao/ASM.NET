using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly ISpotifyService _spotifyService;
    private readonly ILyricsService _lyricsService;
    private readonly IBackgroundQueue _backgroundQueue;

    public SongService(IUnitOfWork unitOfWork, IYoutubeService youtubeService, IWikipediaService wikipediaService, ISpotifyService spotifyService, ILyricsService lyricsService, IBackgroundQueue backgroundQueue)
    {
        _unitOfWork = unitOfWork;
        _youtubeService = youtubeService;
        _wikipediaService = wikipediaService;
        _spotifyService = spotifyService;
        _lyricsService = lyricsService;
        _backgroundQueue = backgroundQueue;
    }

    public async Task<IEnumerable<SongDto>> GetAllSongsAsync()
    {
        var songs = await _unitOfWork.Repository<Song>().GetAllAsync();
        var allGenres = await _unitOfWork.Repository<Genre>().GetAllAsync();
        var allSongGenres = await _unitOfWork.Repository<SongGenre>().GetAllAsync();

        return songs.Select(s => new SongDto
        {
            SongId = s.SongId,
            Title = s.Title,
            Duration = s.Duration,
            Isrc = s.Isrc,
            YoutubeVideoId = s.YoutubeVideoId,
            LyricsText = s.LyricsText,
            LyricsSyncUrl = s.LyricsSyncUrl,
            ThumbnailUrl = s.ThumbnailUrl,
            IsExplicit = s.IsExplicit,
            PlayCount = s.PlayCount,
            IsPremiumOnly = s.IsPremiumOnly,
            AlbumId = s.AlbumId,
            ReleaseDate = s.ReleaseDate,
            GenreNames = allSongGenres.Where(sg => sg.SongId == s.SongId)
                                      .Join(allGenres, sg => sg.GenreId, g => g.GenreId, (sg, g) => g.Name)
        });
    }

    public async Task<(IEnumerable<SongDto> Songs, int TotalCount)> GetPaginatedSongsAsync(int page, int pageSize, string? searchTerm = null)
    {
        var query = _unitOfWork.Repository<Song>().Query();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(s => s.Title.Contains(searchTerm));
        }

        int totalCount = await query.CountAsync();
        var songs = await query.OrderByDescending(s => s.SongId)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();

        var allGenres = await _unitOfWork.Repository<Genre>().GetAllAsync();
        var allSongGenres = await _unitOfWork.Repository<SongGenre>().GetAllAsync();

        var dtos = songs.Select(s => new SongDto
        {
            SongId = s.SongId,
            Title = s.Title,
            Duration = s.Duration,
            YoutubeVideoId = s.YoutubeVideoId,
            ThumbnailUrl = s.ThumbnailUrl,
            IsExplicit = s.IsExplicit,
            PlayCount = s.PlayCount,
            IsPremiumOnly = s.IsPremiumOnly,
            GenreNames = allSongGenres.Where(sg => sg.SongId == s.SongId)
                                      .Join(allGenres, sg => sg.GenreId, g => g.GenreId, (sg, g) => g.Name)
        });

        return (dtos, totalCount);
    }

    public async Task<SongDto?> GetSongByIdAsync(int id)
    {
        var s = await _unitOfWork.Repository<Song>().Query()
            .Include(s => s.SongArtists)
                .ThenInclude(sa => sa.Artist)
            .FirstOrDefaultAsync(s => s.SongId == id);

        if (s == null) return null;
        var genreIds = _unitOfWork.Repository<SongGenre>().Find(sg => sg.SongId == id).Select(sg => sg.GenreId).ToList();

        return new SongDto 
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
            GenreIds = genreIds,
            AuthorName = s.SongArtists.FirstOrDefault()?.Artist?.Name ?? "Nghệ sĩ",
            AuthorBio = s.SongArtists.FirstOrDefault()?.Artist?.Bio ?? "Thông tin nghệ sĩ đang được cập nhật...",
            LyricsText = s.LyricsText
        };
    }

    public async Task<IEnumerable<SongDto>> GetSongsByIdsAsync(IEnumerable<int> ids)
    {
        var songs = await _unitOfWork.Repository<Song>().Query()
            .AsNoTracking()
            .Include(s => s.SongArtists)
                .ThenInclude(sa => sa.Artist)
            .Where(s => ids.Contains(s.SongId))
            .ToListAsync();

        return songs.Select(s => new SongDto
        {
            SongId = s.SongId,
            Title = s.Title,
            Duration = s.Duration,
            YoutubeVideoId = s.YoutubeVideoId,
            ThumbnailUrl = s.ThumbnailUrl,
            AuthorName = s.SongArtists.FirstOrDefault()?.Artist?.Name ?? "Nghệ sĩ",
            AuthorBio = s.SongArtists.FirstOrDefault()?.Artist?.Bio ?? "Thông tin nghệ sĩ đang được cập nhật...",
            AlbumId = s.AlbumId
        }).OrderBy(s => ids.ToList().IndexOf(s.SongId));
    }

    public async Task CreateSongAsync(SongDto dto)
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
        await _unitOfWork.Repository<Song>().AddAsync(s);
        await _unitOfWork.CompleteAsync();

        if (dto.GenreIds != null && dto.GenreIds.Any())
        {
            foreach (var gid in dto.GenreIds)
            {
                await _unitOfWork.Repository<SongGenre>().AddAsync(new SongGenre { SongId = s.SongId, GenreId = gid });
            }
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task ImportFromYoutubeAsync(string videoUrl)
    {
        await ImportAndReturnSongAsync(videoUrl);
    }

    public async Task<SongDto?> GetOrCreateByYoutubeIdAsync(string youtubeId)
    {
        var existing = await _unitOfWork.Repository<Song>().FirstOrDefaultAsync(s => s.YoutubeVideoId == youtubeId);
        if (existing != null) return await GetSongByIdAsync(existing.SongId);

        var imported = await ImportAndReturnSongAsync($"https://youtube.com/watch?v={youtubeId}");
        return imported != null ? await GetSongByIdAsync(imported.SongId) : null;
    }

    public async Task<SongDto?> ImportAndReturnSongAsync(string videoUrl)
    {
        string? youtubeId = null;
        try {
            if (videoUrl.Contains("v=")) {
                youtubeId = videoUrl.Split("v=").Last().Split("&").First();
            } else if (videoUrl.Contains("youtu.be/")) {
                youtubeId = videoUrl.Split("youtu.be/").Last().Split("?").First();
            }
        } catch { }

        if (string.IsNullOrEmpty(youtubeId)) {
            Console.WriteLine($"[SongService] Invalid YouTube URL: {videoUrl}");
            return null;
        }

        try {

            var existingSong = await _unitOfWork.Repository<Song>().FirstOrDefaultAsync(s => s.YoutubeVideoId == youtubeId);
            if (existingSong != null) return await GetSongByIdAsync(existingSong.SongId);

        var details = await _youtubeService.GetVideoDetailsAsync(videoUrl);

        // ─── Spotify Enrichment ────────────────────────────────────────────────
        // Call Spotify with cleaned title + artist to get real metadata.
        // This is non-blocking for the user — we still save immediately, then enrich.
        var spotifyTrack = await _spotifyService.SearchTrackAsync(details.CleanedTitle, details.CleanedArtist);
        
        if (spotifyTrack != null)
        {
            Console.WriteLine($"[Spotify] Enriched: {details.Title} → {spotifyTrack.ArtistName} / {spotifyTrack.AlbumName} / Genres: {string.Join(", ", spotifyTrack.Genres)}");
        }

        // ─── Determine Genre from Spotify or fallback to heuristic ────────────
        string genreName = spotifyTrack?.Genres.FirstOrDefault() 
                           ?? details.Genre  // GuessGenre() heuristic fallback
                           ?? "General";

        // ─── Normalize genre name to our internal categories ──────────────────
        genreName = NormalizeGenreName(genreName);

        // ─── Map Artist Name: prefer Spotify's verified name ──────────────────
        string artistDisplayName = spotifyTrack?.ArtistName ?? details.CleanedArtist ?? details.AuthorName;

        var artist = await _unitOfWork.Repository<Artist>().FirstOrDefaultAsync(a => a.Name.ToLower() == artistDisplayName.ToLower());
        bool isNewArtist = (artist == null);

        if (isNewArtist)
        {
            // Prefer Spotify avatar if available, else YouTube channel avatar
            string avatarUrl = (!string.IsNullOrEmpty(spotifyTrack?.SpotifyArtistId))
                ? (await _spotifyService.GetArtistInfoAsync(spotifyTrack.SpotifyArtistId))?.ImageUrl ?? details.AuthorAvatarUrl
                : details.AuthorAvatarUrl;

            var wikipediaBio = await _wikipediaService.GetArtistBioAsync(artistDisplayName);

            artist = new Artist { 
                Name = artistDisplayName,
                AvatarUrl = avatarUrl,
                Bio = wikipediaBio ?? "Artist automatically imported from YouTube Music Player.",
                SubscriberCount = 0
            };
            await _unitOfWork.Repository<Artist>().AddAsync(artist);
            await _unitOfWork.CompleteAsync(); 
        }
        else if (string.IsNullOrEmpty(artist!.AvatarUrl) && !string.IsNullOrEmpty(spotifyTrack?.SpotifyArtistId))
        {
            // Backfill avatar if it was missing
            var spotifyArtist = await _spotifyService.GetArtistInfoAsync(spotifyTrack.SpotifyArtistId);
            if (!string.IsNullOrEmpty(spotifyArtist?.ImageUrl))
            {
                artist.AvatarUrl = spotifyArtist.ImageUrl;
                _unitOfWork.Repository<Artist>().Update(artist);
                await _unitOfWork.CompleteAsync();
            }
        }

        // ─── Fetch Lyrics ───
        string? lyrics = await _lyricsService.GetLyricsAsync(artistDisplayName, spotifyTrack?.TrackName ?? details.Title);

        // SAVE THE MAIN SONG — now enriched with Spotify & Lyrics
        var song = new Song
        {
            Title = spotifyTrack?.TrackName ?? details.Title,
            YoutubeVideoId = youtubeId,
            ThumbnailUrl = details.ThumbnailUrl,
            Duration = (int?)(details.Duration?.TotalSeconds),
            IsExplicit = spotifyTrack?.IsExplicit ?? false,
            LyricsText = lyrics,
            PlayCount = (int)(details.ViewCount / 10000), // Seed with popularity
            ReleaseDate = DateTime.TryParse(spotifyTrack?.ReleaseDate, out var rd) ? DateTime.SpecifyKind(rd, DateTimeKind.Utc) : DateTime.UtcNow,
        };
        await _unitOfWork.Repository<Song>().AddAsync(song);
        await _unitOfWork.CompleteAsync();

        // LINK GENRE — use Spotify genre (already normalized) or heuristic fallback
        if (!string.IsNullOrEmpty(genreName))
        {
            var genreEntity = await _unitOfWork.Repository<Genre>().FirstOrDefaultAsync(g => g.Name.ToLower() == genreName.ToLower());
            if (genreEntity == null)
            {
                genreEntity = new Genre { Name = genreName };
                await _unitOfWork.Repository<Genre>().AddAsync(genreEntity);
                await _unitOfWork.CompleteAsync();
            }
            await _unitOfWork.Repository<SongGenre>().AddAsync(new SongGenre { SongId = song.SongId, GenreId = genreEntity.GenreId });
            await _unitOfWork.CompleteAsync();
        }

        var songArtist = new SongArtist { SongId = song.SongId, ArtistId = artist!.ArtistId, Role = "Main" };
        await _unitOfWork.Repository<SongArtist>().AddAsync(songArtist);
        await _unitOfWork.CompleteAsync();

        // ─── Artist Enrichment (If exists but needs bio) ───
        if (!isNewArtist && (string.IsNullOrEmpty(artist!.Bio) || artist.Bio.Contains("automatically imported")))
        {
            var wikipediaBio = await _wikipediaService.GetArtistBioAsync(details.AuthorName);
            if (!string.IsNullOrEmpty(wikipediaBio))
            {
                artist.Bio = wikipediaBio;
                _unitOfWork.Repository<Artist>().Update(artist);
                await _unitOfWork.CompleteAsync();
            }
        }

        return await GetSongByIdAsync(song.SongId);
    } 
    catch (Exception ex) {
        Console.WriteLine($"[SongService] Critical error importing {videoUrl}: {ex.Message}");
    }
    return null;
}

    public async Task UpdateSongAsync(SongDto dto)
    {
        var s = await _unitOfWork.Repository<Song>().GetByIdAsync(dto.SongId);
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
            
            var existing = _unitOfWork.Repository<SongGenre>().Find(sg => sg.SongId == s.SongId).ToList();
            foreach (var sg in existing) _unitOfWork.Repository<SongGenre>().Remove(sg);
            if (dto.GenreIds != null) {
                foreach (var gid in dto.GenreIds) {
                    await _unitOfWork.Repository<SongGenre>().AddAsync(new SongGenre { SongId = s.SongId, GenreId = gid });
                }
            }
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task DeleteSongAsync(int id)
    {
        var s = await _unitOfWork.Repository<Song>().GetByIdAsync(id);
        if (s != null)
        {
            var relations = _unitOfWork.Repository<SongArtist>().Find(sa => sa.SongId == id).ToList();
            foreach (var r in relations) _unitOfWork.Repository<SongArtist>().Remove(r);
            _unitOfWork.Repository<Song>().Remove(s);
            await _unitOfWork.CompleteAsync();
        }
    }

    /// <summary>
    /// Map Spotify's English genre tags → our internal category names.
    /// Spotify returns tags like "k-pop", "pop", "edm", "viet pop", etc.
    /// </summary>
    private static string NormalizeGenreName(string genre)
    {
        var g = genre.ToLower().Trim();

        if (g.Contains("k-pop") || g.Contains("kpop") || g.Contains("korean"))   return "K-Pop";
        if (g.Contains("viet") || g.Contains("v-pop") || g.Contains("vpop"))      return "Nhạc Trẻ";
        if (g.Contains("ballad"))                                                   return "Ballad";
        if (g.Contains("classic") || g.Contains("orchestra") || g.Contains("symphony")) return "Nhạc Classic";
        if (g.Contains("edm") || g.Contains("remix") || g.Contains("house") || g.Contains("vinahouse") || g.Contains("dance") || g.Contains("electronic")) return "Remix";
        if (g.Contains("pop"))                                                      return "Nhạc Pop";
        if (g.Contains("r&b") || g.Contains("rnb") || g.Contains("rap") || g.Contains("hip-hop") || g.Contains("hip hop")) return "US-UK";
        if (g.Contains("rock") || g.Contains("metal") || g.Contains("indie"))     return "US-UK";

        // If it's not Vietnamese content but is a known Western genre → US-UK umbrella
        return genre.Length > 0 ? genre : "General";
    }

    public async Task<Dictionary<string, long>> GetUniversalPlayCountsAsync()
    {
        return await _unitOfWork.Repository<Song>().Query()
            .Where(s => !string.IsNullOrEmpty(s.YoutubeVideoId))
            .ToDictionaryAsync(s => s.YoutubeVideoId, s => s.PlayCount);
    }

    public async Task<IEnumerable<SongDto>> GetTrendingSongsAsync(int count = 10)
    {
        try {
            // MIX 1: Real-time Trending from YouTube (Hits)
            var hits = await _youtubeService.GetTrendingMusicAsync(count);
            var hitDtos = hits
                .Where(h => !string.IsNullOrEmpty(h.YoutubeVideoId) && !string.IsNullOrEmpty(h.ThumbnailUrl))
                .Select(h => new SongDto {
                    Title = h.Title,
                    YoutubeVideoId = h.YoutubeVideoId,
                    ThumbnailUrl = h.ThumbnailUrl,
                    AuthorName = h.AuthorName,
                    PlayCount = h.ViewCount / 1000
                }).ToList();

            // MIX 2: Top played from DB — only songs with valid YouTube IDs and real playcounts
            var dbSongs = await _unitOfWork.Repository<Song>().Query()
                .Where(s => !string.IsNullOrEmpty(s.YoutubeVideoId)
                         && !string.IsNullOrEmpty(s.ThumbnailUrl)
                         && s.PlayCount > 0)
                .OrderByDescending(s => s.PlayCount)
                .Take(count)
                .ToListAsync();
            
            var allGenres = await _unitOfWork.Repository<Genre>().GetAllAsync();
            var allSongGenres = await _unitOfWork.Repository<SongGenre>().GetAllAsync();

            var dbDtos = dbSongs.Select(s => new SongDto
            {
                SongId = s.SongId,
                Title = s.Title,
                YoutubeVideoId = s.YoutubeVideoId,
                ThumbnailUrl = s.ThumbnailUrl,
                PlayCount = s.PlayCount,
                IsExplicit = s.IsExplicit,
                GenreNames = allSongGenres.Where(sg => sg.SongId == s.SongId)
                                        .Join(allGenres, sg => sg.GenreId, g => g.GenreId, (sg, g) => g.Name)
            });

            // Merge: Hits first, then fill with DB top songs (no duplicates)
            return hitDtos.Concat(dbDtos.Where(d => !hitDtos.Any(h => h.YoutubeVideoId == d.YoutubeVideoId)))
                          .Take(count);
        } catch {
             // Rollback to classic DB only — still filtered
             var songs = await _unitOfWork.Repository<Song>().Query()
                 .Where(s => !string.IsNullOrEmpty(s.YoutubeVideoId) && !string.IsNullOrEmpty(s.ThumbnailUrl))
                 .OrderByDescending(s => s.PlayCount)
                 .Take(count)
                 .ToListAsync();
             return songs.Select(s => new SongDto { SongId = s.SongId, Title = s.Title, YoutubeVideoId = s.YoutubeVideoId, ThumbnailUrl = s.ThumbnailUrl });
        }
    }
}
