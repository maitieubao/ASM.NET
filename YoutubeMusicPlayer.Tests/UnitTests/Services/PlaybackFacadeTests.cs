using NUnit.Framework;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Domain.Entities;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace YoutubeMusicPlayer.Tests.UnitTests.Services;

[TestFixture]
public class PlaybackFacadeTests
{
    private Mock<IYoutubeService> _mockYoutube;
    private Mock<ISongService> _mockSong;
    private Mock<ILyricsService> _mockLyrics;
    private Mock<IInteractionService> _mockInteraction;
    private Mock<ISubscriptionService> _mockSub;
    private Mock<IAuthService> _mockAuth;
    private Mock<IBackgroundQueue> _mockQueue;
    private Mock<IServiceScopeFactory> _mockScopeFactory;
    private IMemoryCache _cache;
    private Mock<ILogger<PlaybackFacade>> _mockLogger;

    private PlaybackFacade _facade;

    [SetUp]
    public void Setup()
    {
        _mockYoutube = new Mock<IYoutubeService>();
        _mockSong = new Mock<ISongService>();
        _mockLyrics = new Mock<ILyricsService>();
        _mockInteraction = new Mock<IInteractionService>();
        _mockSub = new Mock<ISubscriptionService>();
        _mockAuth = new Mock<IAuthService>();
        _mockQueue = new Mock<IBackgroundQueue>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<PlaybackFacade>>();

        // Setup ScopeFactory for parallel DB calls (simulating thread-safe context creation)
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        // Inject services into the scope provider so that parallel tasks can resolve them
        mockServiceProvider.Setup(sp => sp.GetService(typeof(ISongService))).Returns(_mockSong.Object);
        mockServiceProvider.Setup(sp => sp.GetService(typeof(ISubscriptionService))).Returns(_mockSub.Object);
        mockServiceProvider.Setup(sp => sp.GetService(typeof(ILyricsService))).Returns(_mockLyrics.Object);

        _facade = new PlaybackFacade(
            _mockYoutube.Object, _mockSong.Object, _mockLyrics.Object, 
            _mockInteraction.Object, _mockSub.Object, _mockAuth.Object, 
            _mockQueue.Object, _mockScopeFactory.Object, _cache, _mockLogger.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _cache?.Dispose();
    }

    [Test]
    public async Task GetStreamAsync_InvalidUrl_ReturnsError()
    {
        // Act
        var result = await _facade.GetStreamAsync("invalid_url", null, null, 1);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Error, Is.EqualTo("InvalidURL"));
    }

    [Test]
    public async Task GetStreamAsync_PremiumSongAndFreeUser_ReturnsPremiumRequired()
    {
        // Arrange
        string videoId = "vid12345678";
        string url = $"https://youtube.com/watch?v={videoId}";
        var songInfo = new SongDto { IsPremiumOnly = true };
        
        _mockSong.Setup(s => s.GetOrCreateByYoutubeIdAsync(videoId)).ReturnsAsync(songInfo);
        _mockSub.Setup(s => s.IsUserPremiumAsync(1)).ReturnsAsync(false); // Free user

        // Act
        var result = await _facade.GetStreamAsync(url, "Song Title", "Artist", 1);

        // Assert
        Assert.That(result.Error, Is.EqualTo("PremiumRequired"));
        Assert.That(result.StreamUrl, Is.Null);
    }

    [Test]
    public async Task GetStreamAsync_PremiumSongAndPremiumUser_ReturnsStreamUrl()
    {
        // Arrange
        string videoId = "premium_v11";
        string url = $"https://youtube.com/watch?v={videoId}";
        var songInfo = new SongDto { IsPremiumOnly = true };
        
        _mockSong.Setup(s => s.GetOrCreateByYoutubeIdAsync(videoId)).ReturnsAsync(songInfo);
        _mockSub.Setup(s => s.IsUserPremiumAsync(99)).ReturnsAsync(true); // Premium user
        
        _mockYoutube.Setup(y => y.GetAudioStreamUrlAsync(videoId, It.IsAny<string>(), It.IsAny<string>(), false)).ReturnsAsync("https://premium.stream.link");

        // Act
        var result = await _facade.GetStreamAsync(url, "VIP Track", "VIP Artist", 99);

        // Assert
        Assert.That(result.Error, Is.Null);
        Assert.That(result.StreamUrl, Is.EqualTo("https://premium.stream.link"));
    }

    [Test]
    public async Task ResolveAndGetStreamAsync_CacheHit_ReturnsFromCache()
    {
        // Arrange - Inject a search result into the cache manually
        string query = "Shape of You";
        var mockResults = new List<YoutubeVideoDetails>
        {
            new YoutubeVideoDetails { YoutubeVideoId = "cached12345", Title = "Shape of You", AuthorName = "Ed Sheeran" }
        };
        string cacheKey = $"yt_search_shape_of_you";
        _cache.Set(cacheKey, mockResults);

        // Setup the next step mock (getting stream)
        var songInfo = new SongDto { IsPremiumOnly = false };
        _mockSong.Setup(s => s.GetOrCreateByYoutubeIdAsync("cached12345")).ReturnsAsync(songInfo);
        _mockYoutube.Setup(y => y.GetAudioStreamUrlAsync("cached12345", It.IsAny<string>(), It.IsAny<string>(), false)).ReturnsAsync("https://cached.stream");

        // Act
        var result = await _facade.ResolveAndGetStreamAsync(query, "Shape of You", "Ed Sheeran", 1);

        // Assert
        Assert.That(result.StreamUrl, Is.EqualTo("https://cached.stream"));
        _mockYoutube.Verify(y => y.SearchVideosAsync(It.IsAny<string>(), 30, false), Times.Never); // Verified Cache was hit, no external search API called
    }
}
