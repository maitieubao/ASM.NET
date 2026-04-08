using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System;

namespace YoutubeMusicPlayer.Tests.UnitTests.Controllers;

[TestFixture]
public class PlaylistControllerTests
{
    private Mock<IPlaylistService> _mockPlaylistService;
    private Mock<ISongService> _mockSongService;
    private Mock<YoutubeMusicPlayer.Domain.Interfaces.IUnitOfWork> _mockUow;
    private Mock<IInteractionService> _mockInteractionService;
    private PlaylistController _controller;

    [SetUp]
    public void Setup()
    {
        _mockPlaylistService = new Mock<IPlaylistService>();
        _mockSongService = new Mock<ISongService>();
        _mockUow = new Mock<YoutubeMusicPlayer.Domain.Interfaces.IUnitOfWork>();
        _mockInteractionService = new Mock<IInteractionService>();
        
        _controller = new PlaylistController(
            _mockPlaylistService.Object, 
            _mockSongService.Object, 
            _mockUow.Object,
            _mockInteractionService.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Customer")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = new DefaultHttpContext() { User = user }
        };
    }

    [TearDown]
    public void TearDown()
    {
        _controller?.Dispose();
    }

    [Test]
    public async Task Index_ReturnsViewWithPlaylists()
    {
        _mockPlaylistService.Setup(s => s.GetUserPlaylistsAsync(1))
            .ReturnsAsync(new List<PlaylistDto> { new PlaylistDto { Title = "My List" } });

        var result = await _controller.Index() as ViewResult;

        Assert.That(result, Is.Not.Null);
        var model = result.Model as IEnumerable<PlaylistDto>;
        Assert.That(model.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Create_ValidTitle_RedirectsToIndex()
    {
        var result = await _controller.Create("New Playlist", "Desc") as RedirectToActionResult;
        Assert.That(result.ActionName, Is.EqualTo("Index"));
        _mockPlaylistService.Verify(s => s.CreatePlaylistAsync(1, "New Playlist", "Desc"), Times.Once);
    }

    [Test]
    public async Task AddSong_ValidRequest_ReturnsOk()
    {
        var result = await _controller.AddSong(1, 10) as OkResult;
        Assert.That(result, Is.Not.Null);
        _mockPlaylistService.Verify(s => s.AddSongToPlaylistAsync(1, 10, 1), Times.Once);
    }

    [Test]
    public async Task Details_PlaylistNotFound_ReturnsNotFound()
    {
        _mockPlaylistService.Setup(s => s.GetPlaylistByIdAsync(99, 1)).ReturnsAsync((PlaylistDto)null);
        var result = await _controller.Details(99);
        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task LikedSongs_ReturnsViewWithLikedSongs()
    {
        _mockInteractionService.Setup(s => s.GetLikedSongIdsAsync(1)).ReturnsAsync(new List<int> { 10 });
        _mockSongService.Setup(s => s.GetSongsByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<SongDto> { new SongDto { Title = "Liked Song" } });

        var result = await _controller.LikedSongs() as ViewResult;

        Assert.That(result, Is.Not.Null);
        var model = result.Model as PlaylistDto;
        Assert.That(model.Title, Is.EqualTo("Bài hát đã thích"));
        Assert.That(model.Songs.Count, Is.EqualTo(1));
    }
}

