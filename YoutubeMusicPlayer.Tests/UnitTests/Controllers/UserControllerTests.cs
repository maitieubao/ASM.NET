using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Collections.Generic;

namespace YoutubeMusicPlayer.Tests.UnitTests.Controllers;

[TestFixture]
public class UserControllerTests
{
    private Mock<IUserService> _mockUser;
    private Mock<IProfileFacade> _mockProfileFacade;
    private Mock<INotificationService> _mockNotification;
    private UserController _controller;

    [SetUp]
    public void Setup()
    {
        _mockUser = new Mock<IUserService>();
        _mockProfileFacade = new Mock<IProfileFacade>();
        _mockNotification = new Mock<INotificationService>();

        _controller = new UserController(
            _mockUser.Object, 
            _mockProfileFacade.Object, 
            _mockNotification.Object);
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { 
            new Claim(ClaimTypes.NameIdentifier, "1") 
        }, "mock"));
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
    }

    [TearDown]
    public void TearDown()
    {
        _controller?.Dispose();
    }

    [Test]
    public async Task Profile_ReturnsView_IfUserExists()
    {
        _mockProfileFacade.Setup(s => s.BuildUserProfileAsync(1))
            .ReturnsAsync(new UserProfileViewModel());

        var result = await _controller.Profile() as ViewResult;

        Assert.That(result, Is.Not.Null);
    }
}

