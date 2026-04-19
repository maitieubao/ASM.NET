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
/// Unit tests for AdminArtistController.
/// Validates pagination, search, create, edit, delete, bio refresh, toggle verified,
/// and artist search functionality.
/// </summary>
public class AdminArtistControllerTests : BaseControllerTest<AdminArtistController>
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<IArtistService> _artistServiceMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    private AdminArtistController BuildController()
    {
        var controller = new AdminArtistController(_artistServiceMock.Object);

        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    // ─── TC-ARTIST-001 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-ARTIST-001: Index with pagination returns correct page data.
    /// 20 total artists, page 2 of 10 → 10 artists returned, TotalPages = 2, CurrentPage = 2.
    /// </summary>
    [Fact]
    public async Task Index_Pagination_ReturnsCorrectPageData()
    {
        // Arrange
        var artists = Enumerable.Range(11, 10)
            .Select(i => new ArtistDto { ArtistId = i, Name = $"Artist {i}" })
            .ToList();

        _artistServiceMock
            .Setup(s => s.GetPaginatedArtistsAsync(2, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((artists, 20));

        var controller = BuildController();

        // Act
        var result = await controller.Index(page: 2, pageSize: 10);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<VibeMusic.Models.Admin.AdminArtistListViewModel>().Subject;

        model.TotalPages.Should().Be(2,
            because: "ceil(20 / 10) = 2 pages");
        model.CurrentPage.Should().Be(2,
            because: "the current page should be 2");
        model.Artists.Count().Should().Be(10,
            because: "page 2 of 10 should return 10 artists");
    }

    // ─── TC-ARTIST-002 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-ARTIST-002: Create with valid artist DTO creates the artist and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Create_ValidArtist_CreatesAndRedirects()
    {
        // Arrange
        _artistServiceMock
            .Setup(s => s.CreateArtistAsync(It.IsAny<ArtistDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();
        var dto = new ArtistDto { Name = "New Artist" };

        // Act
        var result = await controller.Create(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after creating an artist the controller should redirect to Index");

        AssertSuccess("Thêm nghệ sĩ mới thành công!");
    }

    // ─── TC-ARTIST-003 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-ARTIST-003: Edit with valid artist DTO updates the artist and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Edit_ValidArtist_UpdatesAndRedirects()
    {
        // Arrange
        _artistServiceMock
            .Setup(s => s.UpdateArtistAsync(It.IsAny<ArtistDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();
        var dto = new ArtistDto { ArtistId = 1, Name = "Updated Artist" };

        // Act
        var result = await controller.Edit(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after updating an artist the controller should redirect to Index");

        AssertSuccess("Cập nhật nghệ sĩ thành công!");
    }

    // ─── TC-ARTIST-004 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-ARTIST-004: Delete with valid artist ID soft-deletes the artist and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Delete_ValidArtist_SoftDeletesAndRedirects()
    {
        // Arrange
        _artistServiceMock
            .Setup(s => s.DeleteArtistAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.Delete(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after deleting an artist the controller should redirect to Index");

        AssertSuccess("Nghệ sĩ đã được xóa.");
    }

    // ─── TC-ARTIST-005 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-ARTIST-005: RefreshArtistBio with a successful response sets TempData["Success"].
    /// </summary>
    [Fact]
    public async Task RefreshArtistBio_Success_SetsSuccessTempData()
    {
        // Arrange
        _artistServiceMock
            .Setup(s => s.RefreshArtistBioAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("New bio text about the artist.");

        var controller = BuildController();

        // Act
        var result = await controller.RefreshArtistBio(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Edit",
            because: "after refreshing bio the controller should redirect to Edit");

        AssertSuccess("Artist biography refreshed successfully.");
    }

    // ─── TC-ARTIST-006 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-ARTIST-006: RefreshArtistBio when service returns null sets TempData["Error"].
    /// </summary>
    [Fact]
    public async Task RefreshArtistBio_NotFound_SetsErrorTempData()
    {
        // Arrange
        _artistServiceMock
            .Setup(s => s.RefreshArtistBioAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var controller = BuildController();

        // Act
        var result = await controller.RefreshArtistBio(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Edit",
            because: "after a failed bio refresh the controller should redirect to Edit");

        AssertError("Could not refresh the artist biography from Wikipedia.");
    }

    // ─── TC-ARTIST-007 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-ARTIST-007: ToggleVerified for a valid artist returns JSON success.
    /// </summary>
    [Fact]
    public async Task ToggleVerified_ValidArtist_ReturnsJsonSuccess()
    {
        // Arrange
        _artistServiceMock
            .Setup(s => s.ToggleVerifiedStatusAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = BuildController();

        // Act
        var result = await controller.ToggleVerified(1);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();

        var successProp = value!.GetType().GetProperty("success");
        successProp.Should().NotBeNull(because: "JSON result should have a 'success' property");
        successProp!.GetValue(value).Should().Be(true,
            because: "ToggleVerified should return { success = true } when artist is found");
    }

    // ─── TC-ARTIST-008 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-ARTIST-008: Search with a search term returns JSON results.
    /// </summary>
    [Fact]
    public async Task Search_WithTerm_ReturnsJsonResults()
    {
        // Arrange
        var artists = new List<ArtistDto>
        {
            new() { ArtistId = 1, Name = "Son Tung MTP" }
        };

        _artistServiceMock
            .Setup(s => s.SearchArtistsAsync("son", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(artists);

        var controller = BuildController();

        // Act
        var result = await controller.Search("son");

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value.Should().BeAssignableTo<IEnumerable<ArtistDto>>().Subject;
        value.Count().Should().Be(1,
            because: "Search should return 1 artist matching 'son'");
    }
}
