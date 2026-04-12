using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Infrastructure.External;
using System.Threading.Tasks;
using System;

namespace YoutubeMusicPlayer.Tests.UnitTests.ExternalServices;

[TestFixture]
public class WikipediaServiceTests
{
    private WikipediaService _wikiService;

    [SetUp]
    public void Setup()
    {
        _wikiService = new WikipediaService(new System.Net.Http.HttpClient());
    }

    [Test]
    public async Task GetArtistBioAsync_EmptyName_ReturnsNull()
    {
        var result = await _wikiService.GetArtistBioAsync("");
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetArtistBioAsync_ValidName_ShouldReturnSomething()
    {
        // Real network call test for robustness
        var result = await _wikiService.GetArtistBioAsync("Taylor Swift");
        Assert.That(result, Is.Not.Null);
    }
}

