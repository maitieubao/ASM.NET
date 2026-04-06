using NUnit.Framework;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace YoutubeMusicPlayer.Tests;

[TestFixture]
public class RecommendationServiceTests
{
    private RecommendationService _recommendationService;
    private Mock<IYoutubeService> _mockYoutube;
    private Mock<IDeezerService> _mockDeezer;
    private Mock<IInteractionService> _mockInteraction;
    private Mock<ISongService> _mockSong;
    private Mock<IMemoryCache> _mockCache;

    [SetUp]
    public void Setup()
    {
        _mockYoutube = new Mock<IYoutubeService>();
        _mockDeezer = new Mock<IDeezerService>();
        _mockInteraction = new Mock<IInteractionService>();
        _mockSong = new Mock<ISongService>();
        _mockCache = new Mock<IMemoryCache>();
        
        object? outValue = null;
        _mockCache.Setup(mc => mc.TryGetValue(It.IsAny<object>(), out outValue)).Returns(false);
        _mockCache.Setup(mc => mc.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());

        _recommendationService = new RecommendationService(_mockYoutube.Object, _mockDeezer.Object, _mockInteraction.Object, _mockSong.Object, _mockCache.Object);
    }

    [Test]
    public async Task GetMoodMusicAsync_ShouldReturnSongs()
    {
        var songs = new List<YoutubeVideoDetails> { new YoutubeVideoDetails { Title = "Chill Song", YoutubeVideoId = "C1" } };
        _mockYoutube.Setup(y => y.SearchVideosAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(songs);
        _mockYoutube.Setup(y => y.IsMusic(It.IsAny<YoutubeVideoDetails>())).Returns(true);

        var result = await _recommendationService.GetMoodMusicAsync("chill");

        Assert.That(result.Count(), Is.AtLeast(1));
    }

    [Test]
    public async Task GetSmartDiscoveryAsync_WithPersonalization_ShouldIncludePersonalizedFlag()
    {
        var video = new YoutubeVideoDetails { Title = "Base Video", CleanedArtist = "Artist A", Genre = "Pop" };
        _mockYoutube.Setup(y => y.GetVideoDetailsAsync(It.IsAny<string>())).ReturnsAsync(video);
        _mockInteraction.Setup(s => s.GetTopPreferredGenresAsync(1, 5)).ReturnsAsync(new List<string> { "Pop" });
        _mockYoutube.Setup(y => y.SearchVideosAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
                    .ReturnsAsync(new List<YoutubeVideoDetails> { new YoutubeVideoDetails { Title = "Rec 1", Genre = "Pop", YoutubeVideoId = "R1", CleanedArtist = "Artist B" } });
        _mockYoutube.Setup(y => y.IsMusic(It.IsAny<YoutubeVideoDetails>())).Returns(true);

        var result = await _recommendationService.GetSmartDiscoveryAsync("V1", 1);

        Assert.That(result.Any(v => v.IsPersonalized), Is.True);
    }

    [Test]
    public async Task GetDailyMixAsync_ColdStart_ReturnsTrending()
    {
        _mockInteraction.Setup(s => s.GetTopPreferredGenresWithWeightsAsync(1, 10)).ReturnsAsync(new Dictionary<string, double>());
        _mockYoutube.Setup(y => y.GetTrendingMusicAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(new List<YoutubeVideoDetails> { new YoutubeVideoDetails { Title = "Trending Song" } });
        _mockYoutube.Setup(y => y.IsMusic(It.IsAny<YoutubeVideoDetails>())).Returns(true);

        var result = await _recommendationService.GetDailyMixAsync(1);

        Assert.That(result.Any(v => v.Title == "Trending Song"), Is.True);
    }

    [Test]
    public async Task GetCompilationsAsync_ShouldOnlyReturnCompilations()
    {
        var results = new List<YoutubeVideoDetails>
        {
            new YoutubeVideoDetails { Title = "Single", YoutubeVideoId = "S1" },
            new YoutubeVideoDetails { Title = "Collection", YoutubeVideoId = "C1" }
        };
        _mockYoutube.Setup(y => y.SearchVideosAsync(It.IsAny<string>(), It.IsAny<int>(), true)).ReturnsAsync(results);
        _mockYoutube.Setup(y => y.IsCompilation(It.IsAny<YoutubeVideoDetails>())).Returns((YoutubeVideoDetails v) => v.Title == "Collection");

        var result = await _recommendationService.GetCompilationsAsync(10);

        Assert.That(result.Count(), Is.EqualTo(1));
        Assert.That(result.First().Title, Is.EqualTo("Collection"));
    }
}
