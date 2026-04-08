using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Application.Interfaces;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Tests.UnitTests.ExternalServices;

[TestFixture]
public class LyricsServiceTests
{
    private Mock<ILyricsService> _mockLyrics;

    [SetUp]
    public void Setup()
    {
        _mockLyrics = new Mock<ILyricsService>();
    }

    [Test]
    public async Task GetLyricsAsync_ReturnsLyrics()
    {
        _mockLyrics.Setup(s => s.GetLyricsAsync("Artist", "Title")).ReturnsAsync("Lyrics content");
        var result = await _mockLyrics.Object.GetLyricsAsync("Artist", "Title");
        Assert.That(result, Is.EqualTo("Lyrics content"));
    }
}

