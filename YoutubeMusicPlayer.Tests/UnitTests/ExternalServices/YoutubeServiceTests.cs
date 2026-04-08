using NUnit.Framework;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using YoutubeMusicPlayer.Infrastructure.External;
using YoutubeMusicPlayer.Application.Interfaces;
using System;
using System.Collections.Generic;

namespace YoutubeMusicPlayer.Tests.UnitTests.ExternalServices;

[TestFixture]
public class YoutubeServiceTests
{
    private Mock<IMemoryCache> _mockCache;
    private Mock<IDeezerService> _mockDeezer;
    private YoutubeService _youtubeService;

    [SetUp]
    public void Setup()
    {
        _mockCache = new Mock<IMemoryCache>();
        _mockDeezer = new Mock<IDeezerService>();
        _youtubeService = new YoutubeService(_mockCache.Object, _mockDeezer.Object);
    }

    [Test]
    public void IsMusic_OfficialTrack_ShouldReturnTrue()
    {
        var details = new YoutubeVideoDetails { Title = "Official Music Video", AuthorName = "Artist Vevo", Duration = TimeSpan.FromMinutes(3) };
        Assert.That(_youtubeService.IsMusic(details), Is.True);
    }

    [Test]
    public void IsMusic_VideoOver7Mins_ShouldReturnFalse()
    {
        var details = new YoutubeVideoDetails { Title = "Music Video", AuthorName = "Artist", Duration = TimeSpan.FromMinutes(7.1) };
        Assert.That(_youtubeService.IsMusic(details), Is.False);
    }

    [Test]
    public void IsCompilation_LongVideo_ShouldReturnTrue()
    {
        var details = new YoutubeVideoDetails { Title = "Chill Music", Duration = TimeSpan.FromMinutes(15) };
        Assert.That(_youtubeService.IsCompilation(details), Is.True);
    }

    [Test]
    public void IsCompilation_ShortVideoWithKeyword_ShouldReturnTrue()
    {
        var details = new YoutubeVideoDetails { Title = "Tuyển tập nhạc trẻ", Duration = TimeSpan.FromMinutes(5) };
        Assert.That(_youtubeService.IsCompilation(details), Is.True);
    }

    [Test]
    public void IsKaraoke_WithKeywords_ShouldReturnTrue()
    {
        var details = new YoutubeVideoDetails { Title = "Song Title Karaoke Beat" };
        Assert.That(_youtubeService.IsKaraoke(details), Is.True);
    }

    [Test]
    public void IsMusic_Karaoke_ShouldReturnFalse()
    {
        var details = new YoutubeVideoDetails { Title = "Song Karaoke", Duration = TimeSpan.FromMinutes(3) };
        Assert.That(_youtubeService.IsMusic(details), Is.False);
    }
}

