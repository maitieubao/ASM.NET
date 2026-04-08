using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace YoutubeMusicPlayer.Tests.UnitTests.Services;

[TestFixture]
public class SongServiceTests
{
    private AppDbContext _context;
    private UnitOfWork _uow;
    private Mock<IYoutubeService> _mockYoutube;
    private Mock<IWikipediaService> _mockWiki;
    private Mock<IDeezerService> _mockDeezer;
    private Mock<ILyricsService> _mockLyrics;
    private Mock<IBackgroundQueue> _mockQueue;
    private SongService _songService;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _uow = new UnitOfWork(_context);

        _mockYoutube = new Mock<IYoutubeService>();
        _mockWiki = new Mock<IWikipediaService>();
        _mockDeezer = new Mock<IDeezerService>();
        _mockLyrics = new Mock<ILyricsService>();
        _mockQueue = new Mock<IBackgroundQueue>();

        _songService = new SongService(_uow, _mockYoutube.Object, _mockWiki.Object, _mockDeezer.Object, _mockLyrics.Object, _mockQueue.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _uow?.Dispose();
        _context?.Dispose();
    }

    [Test]
    public async Task GetAllSongsAsync_ShouldReturnMappedDtosWithGenres()
    {
        var song = new Song { SongId = 1, Title = "Song 1" };
        var genre = new Genre { GenreId = 1, Name = "Pop" };
        var songGenre = new SongGenre { SongId = 1, GenreId = 1 };

        await _context.Songs.AddAsync(song);
        await _context.Genres.AddAsync(genre);
        await _context.SongGenres.AddAsync(songGenre);
        await _context.SaveChangesAsync();

        var result = await _songService.GetAllSongsAsync();

        Assert.That(result.Count(), Is.EqualTo(1));
        Assert.That(result.First().GenreNames.First(), Is.EqualTo("Pop"));
    }

    [Test]
    public async Task GetSongByIdAsync_ExistingSong_ShouldReturnSongDto()
    {
        var song = new Song { SongId = 1, Title = "Test Song" };
        await _context.Songs.AddAsync(song);
        await _context.SaveChangesAsync();

        var result = await _songService.GetSongByIdAsync(1);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Title, Is.EqualTo("Test Song"));
    }

    [Test]
    public async Task ImportFromYoutubeAsync_InvalidUrl_ShouldNotAddSong()
    {
        await _songService.ImportFromYoutubeAsync("invalid-url");
        Assert.That(await _context.Songs.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetTrendingSongsAsync_YoutubeFails_ShouldFallbackToDB()
    {
        _mockYoutube.Setup(y => y.GetTrendingMusicAsync(It.IsAny<int>())).ThrowsAsync(new Exception("API Fail"));
        var song = new Song { Title = "DB Trend", YoutubeVideoId = "abc", ThumbnailUrl = "thumb", PlayCount = 100 };
        await _context.Songs.AddAsync(song);
        await _context.SaveChangesAsync();

        var result = await _songService.GetTrendingSongsAsync(1);

        Assert.That(result.Count(), Is.AtLeast(1));
        Assert.That(result.First().Title, Is.EqualTo("DB Trend"));
    }

    [Test]
    public async Task GetSongsByIdsAsync_ReturnsOnlyFoundSongs()
    {
        await _context.Songs.AddAsync(new Song { SongId = 10, Title = "Found" });
        await _context.SaveChangesAsync();

        var result = await _songService.GetSongsByIdsAsync(new[] { 10, 99 });
        Assert.That(result.Count(), Is.EqualTo(1));
        Assert.That(result.First().SongId, Is.EqualTo(10));
    }

    [Test]
    public async Task GetPaginatedSongsAsync_ReturnsCorrectPage()
    {
        for(int i=1; i<=20; i++) await _context.Songs.AddAsync(new Song { Title = $"S{i}" });
        await _context.SaveChangesAsync();

        var (songs, total) = await _songService.GetPaginatedSongsAsync(2, 5);
        Assert.That(songs.Count(), Is.EqualTo(5));
        Assert.That(total, Is.EqualTo(20));
    }

    [Test]
    public async Task GetUniversalPlayCountsAsync_ReturnsDictionary()
    {
        await _context.Songs.AddAsync(new Song { YoutubeVideoId = "vid1", PlayCount = 500 });
        await _context.SaveChangesAsync();

        var result = await _songService.GetUniversalPlayCountsAsync();
        Assert.That(result["vid1"], Is.EqualTo(500));
    }
}

