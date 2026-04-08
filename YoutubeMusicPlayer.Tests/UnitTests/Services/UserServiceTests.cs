using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Domain.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Linq.Expressions;

namespace YoutubeMusicPlayer.Tests.UnitTests.Services;

[TestFixture]
public class UserServiceTests
{
    private UserService _userService;
    private Mock<IUnitOfWork> _mockUnitOfWork;
    private Mock<IGenericRepository<User>> _mockUserRepo;
    private Mock<IGenericRepository<ListeningHistory>> _mockHistoryRepo;
    private Mock<IGenericRepository<Song>> _mockSongRepo;

    [SetUp]
    public void Setup()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockUserRepo = new Mock<IGenericRepository<User>>();
        _mockHistoryRepo = new Mock<IGenericRepository<ListeningHistory>>();
        _mockSongRepo = new Mock<IGenericRepository<Song>>();

        _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(_mockUserRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<ListeningHistory>()).Returns(_mockHistoryRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Song>()).Returns(_mockSongRepo.Object);

        _userService = new UserService(_mockUnitOfWork.Object);
    }

    [Test]
    public async Task GetUserByIdAsync_ExistingUser_ShouldReturnUserDto()
    {
        var user = new User { UserId = 1, Username = "testuser", Email = "test@example.com" };
        _mockUserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        var result = await _userService.GetUserByIdAsync(1);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.UserId, Is.EqualTo(1));
    }

    [Test]
    public async Task GetUserByIdAsync_NonExistentUser_ShouldReturnNull()
    {
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((User)null);
        var result = await _userService.GetUserByIdAsync(99);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ToggleUserLockAsync_ShouldFlipLockStatus()
    {
        var user = new User { UserId = 1, IsLocked = false };
        _mockUserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        var result = await _userService.ToggleUserLockAsync(1);

        Assert.That(result, Is.True);
        Assert.That(user.IsLocked, Is.True);
        _mockUserRepo.Verify(r => r.Update(user), Times.Once);
    }

    [Test]
    public async Task SearchUsersAsync_ShouldReturnFilteredUsers()
    {
        var users = new List<User>
        {
            new User { Username = "alice", Email = "alice@example.com" }
        };
        _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(users.AsQueryable());

        var result = await _userService.SearchUsersAsync("alice");

        Assert.That(result.Count(), Is.EqualTo(1));
    }
}

