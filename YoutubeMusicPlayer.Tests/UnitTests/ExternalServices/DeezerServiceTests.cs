using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Infrastructure.External;
using Microsoft.Extensions.Caching.Memory;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Threading;
using System;
using System.Linq;

namespace YoutubeMusicPlayer.Tests.UnitTests.ExternalServices;

[TestFixture]
public class DeezerServiceTests
{
    private Mock<IMemoryCache> _mockCache;
    private DeezerService _deezerService;
    private HttpClient _httpClient;

    [SetUp]
    public void Setup()
    {
        _mockCache = new Mock<IMemoryCache>();
        
        // Mocking HttpClient to avoid real network calls and null refs
        var handler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(handler);
        _deezerService = new DeezerService(_httpClient, _mockCache.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    public async Task SearchTracksAsync_ReturnsEmptyOnEmptyQuery()
    {
        var result = await _deezerService.SearchTracksAsync("", 1);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetArtistAlbumsAsync_ReturnsEmptyOnEmptyId()
    {
        var result = await _deezerService.GetArtistAlbumsAsync("", 1);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetNewReleasesAsync_AlwaysReturnsNonNull()
    {
        var result = await _deezerService.GetNewReleasesAsync(1);
        Assert.That(result, Is.Not.Null);
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"data\":[]}")
        });
    }
}

