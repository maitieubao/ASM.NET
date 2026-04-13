using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using System.Security.Claims;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Tests.UnitTests.Controllers;

[TestFixture]
public class InteractionControllerTests
{
    private Mock<IInteractionService> _mockInteraction;
    private Mock<ISongService> _mockSong;
    private Mock<IBackgroundQueue> _mockQueue;
    private InteractionController _interController;

    [SetUp]
    public void Setup()
    {
        _mockInteraction = new Mock<IInteractionService>();
        _mockSong = new Mock<ISongService>();
        _mockQueue = new Mock<IBackgroundQueue>();
        
        _interController = new InteractionController(
            _mockInteraction.Object, 
            _mockSong.Object, 
            _mockQueue.Object);
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { 
            new Claim(ClaimTypes.NameIdentifier, "1") 
        }, "mock"));
        _interController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
    }

    [TearDown]
    public void TearDown()
    {
        _interController?.Dispose();
    }

    [Test]
    public async Task ToggleLike_ReturnsJson()
    {
        _mockInteraction.Setup(s => s.ToggleLikeAsync(1, 101)).ReturnsAsync(true);
        var result = await _interController.ToggleLike(101) as OkObjectResult;
        Assert.That(result, Is.Not.Null);
    }
}

