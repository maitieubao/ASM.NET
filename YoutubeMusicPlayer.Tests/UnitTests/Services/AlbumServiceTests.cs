using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using YoutubeMusicPlayer.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace YoutubeMusicPlayer.Tests.UnitTests.Services;

[TestFixture]
public class AlbumServiceTests
{
    private AppDbContext _context;
    private UnitOfWork _uow;
    private Mock<IYoutubeService> _mockYoutube;
    private Mock<IDeezerService> _mockDeezer;
    private Mock<IMemoryCache> _mockCache;
    private Mock<IBackgroundQueue> _mockBackgroundQueue;
    private Mock<IServiceScopeFactory> _mockScopeFactory;
    private Mock<Microsoft.Extensions.Logging.ILogger<AlbumService>> _mockLogger;
    private AlbumService _albumService;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _uow = new UnitOfWork(_context);
        _mockYoutube = new Mock<IYoutubeService>();
        _mockDeezer = new Mock<IDeezerService>();
        _mockCache = new Mock<IMemoryCache>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockBackgroundQueue = new Mock<IBackgroundQueue>();
        _mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<AlbumService>>();

        _albumService = new AlbumService(
            _uow, 
            _mockYoutube.Object, 
            _mockDeezer.Object, 
            _mockCache.Object, 
            _mockScopeFactory.Object,
            _mockBackgroundQueue.Object,
            _mockLogger.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _uow?.Dispose();
        _context?.Dispose();
    }

    [Test]
    public async Task GetAllAlbumsAsync_ReturnsSortedAlbums()
    {
        await _context.Albums.AddRangeAsync(new[] { 
            new Album { Title = "Old", ReleaseDate = DateTime.UtcNow.AddYears(-1) },
            new Album { Title = "New", ReleaseDate = DateTime.UtcNow }
        });
        await _context.SaveChangesAsync();

        var result = await _albumService.GetAllAlbumsAsync();

        Assert.That(result.First().Title, Is.EqualTo("New"));
    }

    [Test]
    public async Task CreateAlbumAsync_AddsToDb()
    {
        var dto = new AlbumDto { Title = "Epic Album", AlbumType = "album" };
        await _albumService.CreateAlbumAsync(dto);
        Assert.That(await _context.Albums.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task GetAlbumByIdAsync_ReturnsDtoWithSongs()
    {
        var album = new Album { AlbumId = 1, Title = "Main", AlbumType = "album" };
        var song = new Song { SongId = 1, Title = "Track 1", AlbumId = 1 };
        await _context.Albums.AddAsync(album);
        await _context.Songs.AddAsync(song);
        await _context.SaveChangesAsync();

        var result = await _albumService.GetAlbumByIdAsync(1);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Title, Is.EqualTo("Main"));
        Assert.That(result.Songs.Count(), Is.EqualTo(1));
    }
}

