using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
/// Unit tests for AdminAlbumController.
/// Validates pagination, search, create, edit, delete, and artist search functionality.
/// </summary>
public class AdminAlbumControllerTests : BaseControllerTest<AdminAlbumController>
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<IAlbumService> _albumServiceMock = new();
    private readonly Mock<IArtistService> _artistServiceMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    private AdminAlbumController BuildController()
    {
        var controller = new AdminAlbumController(
            _albumServiceMock.Object,
            _artistServiceMock.Object);

        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    // ─── TC-ALBUM-001 ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-ALBUM-001: Index with pagination returns correct page data.
    /// 12 total albums, page 1 of 10 → 10 albums returned, TotalPages = 2.
    /// </summary>
    [Fact]
    public async Task Index_Pagination_ReturnsCorrectPageData()
    {
        // Arrange
        var albums = Enumerable.Range(1, 10)
            .Select(i => new AlbumDto { AlbumId = i, Title = $"Album {i}" })
            .ToList();

        _albumServiceMock
            .Setup(s => s.GetPaginatedAlbumsAsync(1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((albums, 12));

        var controller = BuildController();

        // Act
        var result = await controller.Index(page: 1, pageSize: 10);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<VibeMusic.Models.Admin.AdminAlbumListViewModel>().Subject;

        model.TotalPages.Should().Be(2,
            because: "ceil(12 / 10) = 2 pages");
        model.Albums.Count().Should().Be(10,
            because: "page 1 of 10 should return 10 albums");
    }

    // ─── TC-ALBUM-001b ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-ALBUM-001b: Index with search term filters albums correctly.
    /// </summary>
    [Fact]
    public async Task Index_Search_FiltersAlbums()
    {
        // Arrange
        var albums = Enumerable.Range(1, 2)
            .Select(i => new AlbumDto { AlbumId = i, Title = $"Rock Album {i}" })
            .ToList();

        _albumServiceMock
            .Setup(s => s.GetPaginatedAlbumsAsync(1, 10, "rock", It.IsAny<CancellationToken>()))
            .ReturnsAsync((albums, 2));

        var controller = BuildController();

        // Act
        var result = await controller.Index(page: 1, pageSize: 10, searchTerm: "rock");

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<VibeMusic.Models.Admin.AdminAlbumListViewModel>().Subject;

        model.SearchTerm.Should().Be("rock",
            because: "the search term should be preserved in the view model");
        model.Albums.Count().Should().Be(2,
            because: "only 2 albums match the search term 'rock'");
    }

    // ─── TC-ALBUM-002 ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-ALBUM-002: Create with valid album DTO creates the album and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Create_ValidAlbum_CreatesAndRedirects()
    {
        // Arrange
        _albumServiceMock
            .Setup(s => s.CreateAlbumAsync(It.IsAny<AlbumDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();
        var dto = new AlbumDto { Title = "Test Album", ArtistId = 1 };

        // Act
        var result = await controller.Create(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after creating an album the controller should redirect to Index");

        AssertSuccess("Thêm album mới thành công!");
    }

    // ─── TC-ALBUM-003 ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-ALBUM-003: Create with invalid model state returns view with errors
    /// and does NOT call CreateAlbumAsync.
    /// </summary>
    [Fact]
    public async Task Create_InvalidModelState_ReturnsViewWithErrors()
    {
        // Arrange
        var controller = BuildController();
        controller.ModelState.AddModelError("Title", "Required");

        // Act
        var result = await controller.Create(new AlbumDto());

        // Assert
        result.Should().BeOfType<ViewResult>(
            because: "invalid model state should return the view, not redirect");

        _albumServiceMock.Verify(
            s => s.CreateAlbumAsync(It.IsAny<AlbumDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "CreateAlbumAsync must NOT be called when model state is invalid");
    }

    // ─── TC-ALBUM-004 ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-ALBUM-004: Edit with valid album DTO updates the album and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Edit_ValidAlbum_UpdatesAndRedirects()
    {
        // Arrange
        _albumServiceMock
            .Setup(s => s.UpdateAlbumAsync(It.IsAny<AlbumDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();
        var dto = new AlbumDto { AlbumId = 1, Title = "Updated Album" };

        // Act
        var result = await controller.Edit(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after updating an album the controller should redirect to Index");

        AssertSuccess("Cập nhật album thành công!");
    }

    // ─── TC-ALBUM-005 ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-ALBUM-005: Delete with valid album ID soft-deletes the album and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Delete_ValidAlbum_SoftDeletesAndRedirects()
    {
        // Arrange
        _albumServiceMock
            .Setup(s => s.DeleteAlbumAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.Delete(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after deleting an album the controller should redirect to Index");

        AssertSuccess("Album đã được xóa.");
    }

    // ─── TC-ALBUM-006 ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-ALBUM-006: SearchArtists with a search term returns JSON results.
    /// </summary>
    [Fact]
    public async Task SearchArtists_WithTerm_ReturnsJsonResults()
    {
        // Arrange
        var artists = new List<ArtistDto>
        {
            new() { ArtistId = 1, Name = "Taylor Swift" },
            new() { ArtistId = 2, Name = "Taylor Hawkins" }
        };

        _artistServiceMock
            .Setup(s => s.SearchArtistsAsync("taylor", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(artists);

        var controller = BuildController();

        // Act
        var result = await controller.SearchArtists("taylor");

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value.Should().BeAssignableTo<IEnumerable<ArtistDto>>().Subject;
        value.Count().Should().Be(2,
            because: "SearchArtists should return 2 artists matching 'taylor'");
    }
}
