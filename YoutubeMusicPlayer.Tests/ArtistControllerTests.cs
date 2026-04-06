using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Threading.Tasks;
using System.Security.Claims;

namespace YoutubeMusicPlayer.Tests;

[TestFixture]
public class ArtistControllerTests
{
    private Mock<IArtistService> _mockArtist;
    private ArtistController _artistController;

    [SetUp]
    public void Setup()
    {
        _mockArtist = new Mock<IArtistService>();
        _artistController = new ArtistController(_mockArtist.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _artistController?.Dispose();
    }

    [Test]
    public async Task Details_ReturnsView_IfArtistExists()
    {
        var artistDto = new ArtistDto { ArtistId = 1, Name = "Test Artist" };
        _mockArtist.Setup(s => s.GetArtistByIdAsync(1, 1, 1, 10)).ReturnsAsync(artistDto);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "mock"));
        _artistController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };

        var result = await _artistController.Details(1) as ViewResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.EqualTo(artistDto));
    }
}
