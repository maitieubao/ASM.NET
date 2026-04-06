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

namespace YoutubeMusicPlayer.Tests;

[TestFixture]
public class UserControllerTests
{
    private Mock<IUserService> _mockUser;
    private Mock<ICommentService> _mockComment;
    private Mock<INotificationService> _mockNotification;
    private Mock<IPlaylistService> _mockPlaylist;
    private Mock<IInteractionService> _mockInteraction;
    private UserController _controller;

    [SetUp]
    public void Setup()
    {
        _mockUser = new Mock<IUserService>();
        _mockComment = new Mock<ICommentService>();
        _mockNotification = new Mock<INotificationService>();
        _mockPlaylist = new Mock<IPlaylistService>();
        _mockInteraction = new Mock<IInteractionService>();

        _controller = new UserController(_mockUser.Object, _mockComment.Object, _mockNotification.Object, _mockPlaylist.Object, _mockInteraction.Object);
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "mock"));
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
        _mockUser.Setup(s => s.GetUserByIdAsync(It.IsAny<int>())).ReturnsAsync(new UserDto { UserId = 1 });
        _mockUser.Setup(s => s.GetUserListeningHistoryAsync(It.IsAny<int>())).ReturnsAsync(new List<ListeningHistoryDto>());
        _mockNotification.Setup(s => s.GetUserNotificationsAsync(It.IsAny<int>())).ReturnsAsync(new List<NotificationDto>());
        _mockPlaylist.Setup(s => s.GetUserPlaylistsAsync(It.IsAny<int>())).ReturnsAsync(new List<PlaylistDto>());
        _mockInteraction.Setup(s => s.GetTopPreferredGenresAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new List<string>());

        var result = await _controller.Profile() as ViewResult;

        Assert.That(result, Is.Not.Null);
    }
}
