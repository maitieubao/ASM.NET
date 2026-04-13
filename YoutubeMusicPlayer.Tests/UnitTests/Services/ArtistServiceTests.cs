using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Tests.UnitTests.Services;

[TestFixture]
public class ArtistServiceTests
{
    private AppDbContext _context;
    private UnitOfWork _uow;
    private Mock<IWikipediaService> _mockWiki;
    private Mock<IDeezerService> _mockDeezer;
    private Mock<IYoutubeService> _mockYoutube;
    private Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    private Mock<IBackgroundQueue> _mockQueue;
    private Mock<Microsoft.Extensions.Logging.ILogger<ArtistService>> _mockLogger;
    private Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory> _mockScopeFactory;
    private ArtistService _artistService;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _uow = new UnitOfWork(_context);
        _mockWiki = new Mock<IWikipediaService>();
        _mockDeezer = new Mock<IDeezerService>();
        _mockYoutube = new Mock<IYoutubeService>();
        
        _cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        _mockQueue = new Mock<IBackgroundQueue>();
        _mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ArtistService>>();
        
        _mockScopeFactory = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        var mockScope = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        
        mockServiceProvider.Setup(x => x.GetService(typeof(IUnitOfWork))).Returns(() => 
        {
            var ctx = new AppDbContext(options);
            ctx.Database.EnsureCreated(); // Ensure schema exists just in case
            return new UnitOfWork(ctx);
        });
        mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(() => mockScope.Object);

        _artistService = new ArtistService(_uow, _mockWiki.Object, _mockDeezer.Object, _mockYoutube.Object, _cache, _mockQueue.Object, _mockLogger.Object, _mockScopeFactory.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _cache?.Dispose();
        _uow?.Dispose();
        _context?.Dispose();
    }

    [Test]
    public async Task GetPaginatedArtistsAsync_ReturnsCorrectCount()
    {
        await _context.Artists.AddRangeAsync(new[] { new Artist { Name = "A1" }, new Artist { Name = "A2" } });
        await _context.SaveChangesAsync();

        var (artists, total) = await _artistService.GetPaginatedArtistsAsync(1, 1);

        Assert.That(total, Is.EqualTo(2));
        Assert.That(artists.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task CreateArtistAsync_FetchesBioIfEmpty()
    {
        _mockWiki.Setup(w => w.GetArtistBioAsync("New Artist")).ReturnsAsync("Bio from wiki");
        
        await _artistService.CreateArtistAsync(new ArtistDto { Name = "New Artist" });

        var artist = await _context.Artists.FirstOrDefaultAsync(a => a.Name == "New Artist");
        Assert.That(artist.Bio, Is.EqualTo("Bio from wiki"));
    }

    [Test]
    public async Task ToggleFollowAsync_IncrementsSubscriberCount()
    {
        var artist = new Artist { ArtistId = 1, Name = "Star", SubscriberCount = 10 };
        await _context.Artists.AddAsync(artist);
        await _context.SaveChangesAsync();

        var result = await _artistService.ToggleFollowAsync(1, 1);

        Assert.That(result, Is.True);
        var updated = await _context.Artists.FindAsync(1);
        Assert.That(updated.SubscriberCount, Is.EqualTo(11));
    }

    [Test]
    public async Task GetArtistByIdAsync_ExistingArtist_ReturnsDtoWithTopSongs()
    {
        var artist = new Artist { ArtistId = 1, Name = "Legends" };
        var song = new Song { SongId = 1, Title = "Hit", PlayCount = 5000 };
        var sa = new SongArtist { ArtistId = 1, SongId = 1 };
        
        await _context.Artists.AddAsync(artist);
        await _context.Songs.AddAsync(song);
        await _context.SongArtists.AddAsync(sa);
        await _context.SaveChangesAsync();
        var result = await _artistService.GetArtistByIdAsync(1);

        Assert.That(result.Name, Is.EqualTo("Legends"));
        Assert.That(result.TopSongs.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task DeleteArtistAsync_MarksIsDeleted()
    {
        var artist = new Artist { ArtistId = 10, Name = "To Delete" };
        await _context.Artists.AddAsync(artist);
        await _context.SaveChangesAsync();

        await _artistService.DeleteArtistAsync(10);

        var deletedArtist = await _context.Artists.FindAsync(10);
        Assert.That(deletedArtist, Is.Not.Null);
        Assert.That(deletedArtist.IsDeleted, Is.True);
    }

    [Test]
    public async Task UpdateArtistAsync_UpdatesAllProperties()
    {
        var artist = new Artist { ArtistId = 5, Name = "Old Name" };
        await _context.Artists.AddAsync(artist);
        await _context.SaveChangesAsync();

        await _artistService.UpdateArtistAsync(new ArtistDto { ArtistId = 5, Name = "New Name", Country = "US" });

        var updated = await _context.Artists.FindAsync(5);
        Assert.That(updated.Name, Is.EqualTo("New Name"));
        Assert.That(updated.Country, Is.EqualTo("US"));
    }

    [Test]
    public async Task RefreshArtistBioAsync_UpdatesDb()
    {
        var artist = new Artist { ArtistId = 7, Name = "Refresher" };
        await _context.Artists.AddAsync(artist);
        await _context.SaveChangesAsync();
        _mockWiki.Setup(w => w.GetArtistBioAsync("Refresher")).ReturnsAsync("Refreshed Bio");

        await _artistService.RefreshArtistBioAsync(7);

        var updated = await _context.Artists.FindAsync(7);
        Assert.That(updated.Bio, Is.EqualTo("Refreshed Bio"));
    }
}

