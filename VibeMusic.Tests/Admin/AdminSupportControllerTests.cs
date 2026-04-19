using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Controllers;
using VibeMusic.Tests.Helpers;
using Xunit;

namespace VibeMusic.Tests.Admin;

/// <summary>
/// Unit tests for AdminSupportController.
/// Validates comment listing, comment deletion, and reports redirect functionality.
/// 
/// Note: AdminSupportController inherits from Controller (not BaseController),
/// so action results are standard ViewResult / RedirectToActionResult.
/// </summary>
public class AdminSupportControllerTests : BaseControllerTest<AdminSupportController>
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<ICommentService> _commentServiceMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    private AdminSupportController BuildController()
    {
        var controller = new AdminSupportController(_commentServiceMock.Object);
        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    // ─── TC-SUPPORT-001 ──────────────────────────────────────────────────────

    /// <summary>
    /// TC-SUPPORT-001: Comments returns a ViewResult whose model contains all comments.
    /// Mock returns 5 comments → model should have 5 items.
    /// </summary>
    [Fact]
    public async Task Comments_ReturnsViewWithAllComments()
    {
        // Arrange
        var comments = Enumerable.Range(1, 5)
            .Select(i => new CommentDto { CommentId = i, Content = $"Comment {i}" })
            .ToList();

        _commentServiceMock
            .Setup(s => s.GetAllCommentsAsync())
            .ReturnsAsync(comments);

        var controller = BuildController();

        // Act
        var result = await controller.Comments();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<CommentDto>>().Subject;
        model.Count().Should().Be(5,
            because: "GetAllCommentsAsync returned 5 comments");
    }

    // ─── TC-SUPPORT-002 ──────────────────────────────────────────────────────

    /// <summary>
    /// TC-SUPPORT-002: DeleteComment with a valid ID deletes the comment and redirects to Comments.
    /// TempData["Success"] should be set to "Comment deleted."
    /// </summary>
    [Fact]
    public async Task DeleteComment_ValidId_DeletesAndRedirects()
    {
        // Arrange
        _commentServiceMock
            .Setup(s => s.DeleteCommentAsync(1, null))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.DeleteComment(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Comments",
            because: "after deleting a comment the controller should redirect to Comments");

        AssertSuccess("Comment deleted.");

        _commentServiceMock.Verify(s => s.DeleteCommentAsync(1, null), Times.Once);
    }

    // ─── TC-SUPPORT-003 ──────────────────────────────────────────────────────

    /// <summary>
    /// TC-SUPPORT-003: Reports redirects to AdminReport/Index.
    /// </summary>
    [Fact]
    public void Reports_RedirectsToAdminReport()
    {
        // Arrange
        var controller = BuildController();

        // Act
        var result = controller.Reports();

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "Reports should redirect to the Index action");
        redirect.ControllerName.Should().Be("AdminReport",
            because: "Reports should redirect to the AdminReport controller");
    }
}
