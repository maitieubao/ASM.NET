using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;

namespace YoutubeMusicPlayer.Tests;

[TestFixture]
public class AdminControllerTests
{
    private Mock<IUserService> _mockUserService;
    private Mock<IDashboardService> _mockDashboard;
    private Mock<ISongService> _mockSong;
    private Mock<IPlaylistService> _mockPlaylist;
    private Mock<INotificationService> _mockNotification;
    private AdminController _controller;

    [SetUp]
    public void Setup()
    {
        _mockUserService = new Mock<IUserService>();
        _mockDashboard = new Mock<IDashboardService>();
        _mockSong = new Mock<ISongService>();
        _mockPlaylist = new Mock<IPlaylistService>();
        _mockNotification = new Mock<INotificationService>();
        
        _controller = new AdminController(
            _mockUserService.Object, null, null, _mockPlaylist.Object, _mockSong.Object,
            _mockDashboard.Object, null, _mockNotification.Object, null, null, null, null
        );

        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.Role, "Admin")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
    }

    [TearDown]
    public void TearDown()
    {
        _controller?.Dispose();
    }

    [Test]
    public async Task Index_ReturnsStatsView()
    {
        _mockDashboard.Setup(s => s.GetStatsAsync()).ReturnsAsync(new DashboardDto());
        var result = await _controller.Index() as ViewResult;
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task SendNotification_Valid_RedirectsToNotifications()
    {
        var result = await _controller.SendNotification("Msg", "User", null) as RedirectToActionResult;
        Assert.That(result.ActionName, Is.EqualTo("Notifications"));
    }

    [Test]
    public async Task CreateFeaturedPlaylist_Valid_RedirectsToFeaturedPlaylists()
    {
        var result = await _controller.CreateFeaturedPlaylist(new YoutubeMusicPlayer.Application.DTOs.PlaylistDto { Title = "Featured" }) as RedirectToActionResult;
        Assert.That(result.ActionName, Is.EqualTo("FeaturedPlaylists"));
    }

    [Test]
    public async Task SendNotification_ToUser_CallsService()
    {
        var result = await _controller.SendNotification("Title", "Msg", 1) as RedirectToActionResult;
        _mockNotification.Verify(s => s.SendUserNotificationAsync(1, "Title", "Msg"), Times.Once);
        Assert.That(result.ActionName, Is.EqualTo("Notifications"));
    }

    [Test]
    public async Task SendNotification_System_CallsService()
    {
        var result = await _controller.SendNotification("Title", "Msg", null) as RedirectToActionResult;
        _mockNotification.Verify(s => s.SendSystemNotificationAsync("Title", "Msg"), Times.Once);
    }

    [Test]
    public async Task ResolveReport_CallsService()
    {
        var result = await _controller.ResolveReport(1, true) as RedirectToActionResult;
        _mockDashboard.Verify(s => s.ResolveReportAsync(1, true), Times.Once);
    }

    [Test]
    public async Task ToggleUserLock_CallsService()
    {
        _mockUserService.Setup(s => s.ToggleUserLockAsync(1)).ReturnsAsync(true);
        var result = await _controller.ToggleUserLock(1) as RedirectToActionResult;
        Assert.That(result.ActionName, Is.EqualTo("Users"));
    }
}
