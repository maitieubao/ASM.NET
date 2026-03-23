using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly IServiceScopeFactory _scopeFactory;

    public SongService(IUnitOfWork unitOfWork, IYoutubeService youtubeService, IWikipediaService wikipediaService, IServiceScopeFactory scopeFactory)
    {
        _unitOfWork = unitOfWork;
        _youtubeService = youtubeService;
        _wikipediaService = wikipediaService;
        _scopeFactory = scopeFactory;
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

    public async Task<SongDto?> GetSongByIdAsync(int id)
    {
        var s = await _unitOfWork.Repository<Song>().GetByIdAsync(id);
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
            GenreIds = genreIds
        };
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
            return;
        }

        bool exists = await _unitOfWork.Repository<Song>().AnyAsync(s => s.YoutubeVideoId == youtubeId);
        if (exists) return;

        var details = await _youtubeService.GetVideoDetailsAsync(videoUrl);

        var artist = await _unitOfWork.Repository<Artist>().FirstOrDefaultAsync(a => a.Name.ToLower() == details.AuthorName.ToLower());
        bool isNewArtist = (artist == null);

        if (isNewArtist)
        {
            var wikipediaBio = await _wikipediaService.GetArtistBioAsync(details.AuthorName);

            artist = new Artist { 
                Name = details.AuthorName,
                AvatarUrl = details.AuthorAvatarUrl,
                Bio = wikipediaBio ?? "Artist automatically imported from YouTube Music Player.",
                SubscriberCount = 0
            };
            await _unitOfWork.Repository<Artist>().AddAsync(artist);
            await _unitOfWork.CompleteAsync(); 
        }

        // SAVE THE MAIN SONG IMMEDIATELY (User doesn't wait for the bulk)
        var song = new Song
        {
            Title = details.Title,
            YoutubeVideoId = youtubeId,
            ThumbnailUrl = details.ThumbnailUrl,
            Duration = (int?)(details.Duration?.TotalSeconds),
            ReleaseDate = DateTime.UtcNow,
            PlayCount = 0
        };
        await _unitOfWork.Repository<Song>().AddAsync(song);
        await _unitOfWork.CompleteAsync();

        // LINK GENRE (Classification)
        if (!string.IsNullOrEmpty(details.Genre))
        {
            var genreEntity = await _unitOfWork.Repository<Genre>().FirstOrDefaultAsync(g => g.Name.ToLower() == details.Genre.ToLower());
            if (genreEntity == null)
            {
                genreEntity = new Genre { Name = details.Genre };
                await _unitOfWork.Repository<Genre>().AddAsync(genreEntity);
                await _unitOfWork.CompleteAsync();
            }
            await _unitOfWork.Repository<SongGenre>().AddAsync(new SongGenre { SongId = song.SongId, GenreId = genreEntity.GenreId });
            await _unitOfWork.CompleteAsync();
        }

        var songArtist = new SongArtist { SongId = song.SongId, ArtistId = artist.ArtistId, Role = "Main" };
        await _unitOfWork.Repository<SongArtist>().AddAsync(songArtist);
        await _unitOfWork.CompleteAsync();

        // BACKGROUND BULK IMPORT (Prevents website freeze)
        if (isNewArtist)
        {
            _ = Task.Run(async () => {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var bgUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var bgYoutubeService = scope.ServiceProvider.GetRequiredService<IYoutubeService>();
                    
                    try 
                    {
                        var channelVideos = await bgYoutubeService.GetChannelVideosAsync(details.AuthorChannelId);
                        foreach (var v in channelVideos)
                        {
                            if (v.YoutubeVideoId == youtubeId) continue;
                            if (!bgYoutubeService.IsMusic(v)) continue;

                            bool songExists = await bgUnitOfWork.Repository<Song>().AnyAsync(s => s.YoutubeVideoId == v.YoutubeVideoId);
                            if (!songExists)
                            {
                                var newSong = new Song {
                                    Title = v.Title,
                                    YoutubeVideoId = v.YoutubeVideoId,
                                    ThumbnailUrl = v.ThumbnailUrl,
                                    Duration = (int?)(v.Duration?.TotalSeconds),
                                    ReleaseDate = DateTime.UtcNow,
                                    PlayCount = 0
                                };
                                    await bgUnitOfWork.Repository<Song>().AddAsync(newSong);
                                    await bgUnitOfWork.CompleteAsync();

                                    // BG LINK GENRE
                                    if (!string.IsNullOrEmpty(v.Genre))
                                    {
                                        var bgGenreEntity = await bgUnitOfWork.Repository<Genre>().FirstOrDefaultAsync(g => g.Name.ToLower() == v.Genre.ToLower());
                                        if (bgGenreEntity == null) {
                                            bgGenreEntity = new Genre { Name = v.Genre };
                                            await bgUnitOfWork.Repository<Genre>().AddAsync(bgGenreEntity);
                                            await bgUnitOfWork.CompleteAsync();
                                        }
                                        await bgUnitOfWork.Repository<SongGenre>().AddAsync(new SongGenre { SongId = newSong.SongId, GenreId = bgGenreEntity.GenreId });
                                    }

                                    await bgUnitOfWork.Repository<SongArtist>().AddAsync(new SongArtist { SongId = newSong.SongId, ArtistId = artist.ArtistId, Role = "Main" });
                                    await bgUnitOfWork.CompleteAsync();
                            }
                        }
                    } 
                    catch (Exception ex) { 
                        Console.WriteLine("Background Import Error: " + ex.Message);
                    }
                }
            });
        }
        else 
        {
            // If the artist already exists but has the "fallback" bio, try to enrich it now
            if (string.IsNullOrEmpty(artist.Bio) || artist.Bio.Contains("automatically imported"))
            {
                var wikipediaBio = await _wikipediaService.GetArtistBioAsync(details.AuthorName);
                if (!string.IsNullOrEmpty(wikipediaBio))
                {
                    artist.Bio = wikipediaBio;
                    _unitOfWork.Repository<Artist>().Update(artist);
                    await _unitOfWork.CompleteAsync();
                }
            }

            var songForArtist = new Song
            {
                Title = details.Title,
                YoutubeVideoId = youtubeId,
                ThumbnailUrl = details.ThumbnailUrl,
                Duration = (int?)(details.Duration?.TotalSeconds),
                ReleaseDate = DateTime.UtcNow,
                PlayCount = 0
            };
            await _unitOfWork.Repository<Song>().AddAsync(songForArtist);
            await _unitOfWork.CompleteAsync();

            if (!string.IsNullOrEmpty(details.Genre)) {
                var artistSongGenreEntity = await _unitOfWork.Repository<Genre>().FirstOrDefaultAsync(g => g.Name.ToLower() == details.Genre.ToLower());
                if (artistSongGenreEntity == null) {
                    artistSongGenreEntity = new Genre { Name = details.Genre };
                    await _unitOfWork.Repository<Genre>().AddAsync(artistSongGenreEntity);
                    await _unitOfWork.CompleteAsync();
                }
                await _unitOfWork.Repository<SongGenre>().AddAsync(new SongGenre { SongId = songForArtist.SongId, GenreId = artistSongGenreEntity.GenreId });
            }

            await _unitOfWork.Repository<SongArtist>().AddAsync(new SongArtist { SongId = songForArtist.SongId, ArtistId = artist.ArtistId, Role = "Main" });
            await _unitOfWork.CompleteAsync();
        }
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
}
