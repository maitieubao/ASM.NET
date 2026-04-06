using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq.Expressions;

namespace YoutubeMusicPlayer.Tests;

[TestFixture]
public class AuthServiceTests
{
    private Mock<IUnitOfWork> _mockUow;
    private Mock<IGenericRepository<User>> _mockUserRepo;
    private AuthService _authService;

    [SetUp]
    public void Setup()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockUserRepo = new Mock<IGenericRepository<User>>();
        _mockUow.Setup(u => u.Repository<User>()).Returns(_mockUserRepo.Object);
        _authService = new AuthService(_mockUow.Object);
    }

    [Test]
    public async Task AuthenticateAsync_ValidCredentials_ReturnsUserDto()
    {
        var password = "password123";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var user = new User { Email = "test@example.com", PasswordHash = hash, IsLocked = false };
        
        _mockUserRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(user);

        var result = await _authService.AuthenticateAsync("test@example.com", password);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Email, Is.EqualTo(user.Email));
    }

    [Test]
    public async Task AuthenticateAsync_LockedUser_ThrowsException()
    {
        var user = new User { Email = "locked@example.com", PasswordHash = "somehash", IsLocked = true };
        _mockUserRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(user);

        var ex = Assert.ThrowsAsync<Exception>(async () => await _authService.AuthenticateAsync("locked@example.com", "any"));
        Assert.That(ex.Message, Does.Contain("locked"));
    }

    [Test]
    public async Task RegisterAsync_ExistingEmail_ThrowsException()
    {
        _mockUserRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(new User());

        var dto = new RegisterDto { Email = "exists@example.com", Password = "pw" };
        var ex = Assert.ThrowsAsync<Exception>(async () => await _authService.RegisterAsync(dto));
        Assert.That(ex.Message, Does.Contain("already registered"));
    }

    [Test]
    public async Task ForgotPasswordAsync_ExistingUser_ReturnsToken()
    {
        var user = new User { Email = "user@example.com" };
        _mockUserRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(user);

        var token = await _authService.ForgotPasswordAsync("user@example.com");

        Assert.That(token, Is.Not.Null);
        Assert.That(user.ResetToken, Is.EqualTo(token));
        _mockUow.Verify(u => u.CompleteAsync(), Times.Once);
    }

    [Test]
    public async Task ResetPasswordAsync_ValidToken_ReturnsTrue()
    {
        var user = new User { Email = "user@example.com", ResetToken = "123456", ResetTokenExpiry = DateTime.UtcNow.AddHours(1) };
        _mockUserRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(user);

        var result = await _authService.ResetPasswordAsync("user@example.com", "123456", "newpassword");

        Assert.That(result, Is.True);
        Assert.That(BCrypt.Net.BCrypt.Verify("newpassword", user.PasswordHash), Is.True);
    }

    [Test]
    public async Task ResetPasswordAsync_ExpiredToken_ReturnsFalse()
    {
        var user = new User { Email = "user@example.com", ResetToken = "123456", ResetTokenExpiry = DateTime.UtcNow.AddHours(-1) };
        _mockUserRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(user);

        var result = await _authService.ResetPasswordAsync("user@example.com", "123456", "newpassword");

        Assert.That(result, Is.False);
    }
}
