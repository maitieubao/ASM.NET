using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure;
using YoutubeMusicPlayer.Domain.Entities;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace YoutubeMusicPlayer.Tests.UnitTests.Services;

[TestFixture]
public class CommentServiceTests
{
    private AppDbContext _context;
    private UnitOfWork _uow;
    private CommentService _commentService;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _uow = new UnitOfWork(_context);
        _commentService = new CommentService(_uow);
    }

    [TearDown]
    public void TearDown()
    {
        _uow?.Dispose();
        _context?.Dispose();
    }

    [Test]
    public async Task Comment_CRUD_WorksAsExpected()
    {
        var user = new User { UserId = 1, Username = "commenter" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var commentDto = await _commentService.CreateCommentAsync(user.UserId, 1, "Cool song!");
        Assert.That(commentDto.Content, Is.EqualTo("Cool song!"));

        await _commentService.UpdateCommentAsync(commentDto.CommentId, user.UserId, "Amazing!");
        var comments = await _commentService.GetSongCommentsAsync(1);
        Assert.That(comments.First().Content, Is.EqualTo("Amazing!"));
    }
}

