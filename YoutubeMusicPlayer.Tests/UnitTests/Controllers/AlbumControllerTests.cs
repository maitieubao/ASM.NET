using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Tests.UnitTests.Controllers;

[TestFixture]
public class AlbumControllerTests
{
    private Mock<IAlbumService> _mockAlbum;
    private Mock<IDeezerService> _mockDeezer;
    private Mock<IITunesService> _mockITunes;
    private AlbumController _albumController;

    [SetUp]
    public void Setup()
    {
        _mockAlbum = new Mock<IAlbumService>();
        _mockDeezer = new Mock<IDeezerService>();
        _mockITunes = new Mock<IITunesService>();
        _albumController = new AlbumController(
            _mockAlbum.Object, 
            _mockDeezer.Object, 
            _mockITunes.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _albumController?.Dispose();
    }

    [Test]
    public async Task Details_ReturnsNotFound_IfAlbumMissing()
    {
        _mockAlbum.Setup(s => s.GetAlbumByIdAsync(999)).ReturnsAsync((AlbumDto)null);

        var result = await _albumController.Details(999);

        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
    }
}

