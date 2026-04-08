using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Security.Claims;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Tests.UnitTests.Controllers;

[TestFixture]
public class CommentControllerTests
{
    private Mock<ICommentService> _mockComment;
    private CommentController _commentController;

    [SetUp]
    public void Setup()
    {
        _mockComment = new Mock<ICommentService>();
        _commentController = new CommentController(_mockComment.Object);
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { 
            new Claim(ClaimTypes.NameIdentifier, "1") 
        }, "mock"));
        _commentController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
    }

    [TearDown]
    public void TearDown()
    {
        _commentController?.Dispose();
    }

    [Test]
    public async Task AddComment_ReturnsJson()
    {
        var comment = new CommentDto { Content = "Nice!" };
        _mockComment.Setup(s => s.CreateCommentAsync(1, 10, "Nice!", null)).ReturnsAsync(comment);
        var result = await _commentController.AddComment(10, "Nice!") as JsonResult;
        Assert.That(result, Is.Not.Null);
    }
}

