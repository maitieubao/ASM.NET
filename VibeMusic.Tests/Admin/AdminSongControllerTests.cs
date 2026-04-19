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
/// Unit tests for AdminSongController.
/// Validates pagination, search, create, edit, delete, toggle premium/explicit,
/// and album search functionality.
/// </summary>
public class AdminSongControllerTests : BaseControllerTest<AdminSongController>
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<ISongService> _songServiceMock = new();
    private readonly Mock<IAlbumService> _albumServiceMock = new();
    private readonly Mock<IGenreService> _genreServiceMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the controller under test, wires up all mocks, sets the admin
    /// ControllerContext and TempData.
    /// </summary>
    private AdminSongController BuildController()
    {
        var controller = new AdminSongController(
            _songServiceMock.Object,
            _albumServiceMock.Object,
            _genreServiceMock.Object);

        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    // ─── TC-SONG-001 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SONG-001: Index with pagination returns correct page data.
    /// 15 total songs, page 1 of 10 → 10 songs returned, TotalPages = 2.
    /// </summary>
    [Fact]
    public async Task Index_Pagination_ReturnsCorrectPageData()
    {
        // Arrange
        var songs = Enumerable.Range(1, 10)
            .Select(i => new SongDto { SongId = i, Title = $"Song {i}" })
            .ToList();

        _songServiceMock
            .Setup(s => s.GetPaginatedSongsAsync(1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((songs, 15));

        var controller = BuildController();

        // Act
        var result = await controller.Index(page: 1, pageSize: 10);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<VibeMusic.Models.Admin.AdminSongListViewModel>().Subject;

        model.TotalPages.Should().Be(2,
            because: "ceil(15 / 10) = 2 pages");
        model.Songs.Count().Should().Be(10,
            because: "page 1 of 10 should return 10 songs");
    }

    // ─── TC-SONG-001b ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SONG-001b: Index with search term filters songs correctly.
    /// </summary>
    [Fact]
    public async Task Index_Search_FiltersSongs()
    {
        // Arrange
        var songs = Enumerable.Range(1, 3)
            .Select(i => new SongDto { SongId = i, Title = $"Love Song {i}" })
            .ToList();

        _songServiceMock
            .Setup(s => s.GetPaginatedSongsAsync(1, 10, "love", It.IsAny<CancellationToken>()))
            .ReturnsAsync((songs, 3));

        var controller = BuildController();

        // Act
        var result = await controller.Index(page: 1, pageSize: 10, searchTerm: "love");

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<VibeMusic.Models.Admin.AdminSongListViewModel>().Subject;

        model.SearchTerm.Should().Be("love",
            because: "the search term should be preserved in the view model");
        model.Songs.Count().Should().Be(3,
            because: "only 3 songs match the search term 'love'");
    }

    // ─── TC-SONG-002 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SONG-002: Create with valid song DTO creates the song and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Create_ValidSong_CreatesAndRedirects()
    {
        // Arrange
        _songServiceMock
            .Setup(s => s.CreateSongAsync(It.IsAny<SongDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _genreServiceMock
            .Setup(s => s.GetAllGenresAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GenreDto>());

        var controller = BuildController();
        var dto = new SongDto { Title = "Test Song", AuthorName = "Artist", YoutubeVideoId = "abc123" };

        // Act
        var result = await controller.Create(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after creating a song the controller should redirect to Index");

        AssertSuccess("Thêm bài hát mới thành công!");
    }

    // ─── TC-SONG-003 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SONG-003: Create with invalid model state returns view with errors
    /// and does NOT call CreateSongAsync.
    /// </summary>
    [Fact]
    public async Task Create_InvalidModelState_ReturnsViewWithErrors()
    {
        // Arrange
        _genreServiceMock
            .Setup(s => s.GetAllGenresAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GenreDto>());

        var controller = BuildController();
        controller.ModelState.AddModelError("Title", "Required");

        // Act
        var result = await controller.Create(new SongDto());

        // Assert
        result.Should().BeOfType<ViewResult>(
            because: "invalid model state should return the view, not redirect");

        _songServiceMock.Verify(
            s => s.CreateSongAsync(It.IsAny<SongDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "CreateSongAsync must NOT be called when model state is invalid");
    }

    // ─── TC-SONG-004 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SONG-004: Edit with valid song DTO updates the song and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Edit_ValidSong_UpdatesAndRedirects()
    {
        // Arrange
        _songServiceMock
            .Setup(s => s.UpdateSongAsync(It.IsAny<SongDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _genreServiceMock
            .Setup(s => s.GetAllGenresAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GenreDto>());

        var controller = BuildController();
        var dto = new SongDto { SongId = 1, Title = "New Title", YoutubeVideoId = "abc123" };

        // Act
        var result = await controller.Edit(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after updating a song the controller should redirect to Index");

        AssertSuccess("Cập nhật bài hát thành công!");
    }

    // ─── TC-SONG-005 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SONG-005: Delete with valid song ID soft-deletes the song and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Delete_ValidSong_SoftDeletesAndRedirects()
    {
        // Arrange
        _songServiceMock
            .Setup(s => s.DeleteSongAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.Delete(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after deleting a song the controller should redirect to Index");

        AssertSuccess("Bài hát đã được xóa.");
    }

    // ─── TC-SONG-006 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SONG-006: TogglePremium for a valid song returns JSON success.
    /// </summary>
    [Fact]
    public async Task TogglePremium_ValidSong_ReturnsJsonSuccess()
    {
        // Arrange
        _songServiceMock
            .Setup(s => s.TogglePremiumStatusAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = BuildController();

        // Act
        var result = await controller.TogglePremium(1);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();

        // Use reflection to check anonymous type property
        var successProp = value!.GetType().GetProperty("success");
        successProp.Should().NotBeNull(because: "JSON result should have a 'success' property");
        successProp!.GetValue(value).Should().Be(true,
            because: "TogglePremium should return { success = true } when song is found");
    }

    // ─── TC-SONG-006b ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SONG-006b: TogglePremium for a non-existent song returns BadRequest.
    /// </summary>
    [Fact]
    public async Task TogglePremium_SongNotFound_ReturnsBadRequest()
    {
        // Arrange
        _songServiceMock
            .Setup(s => s.TogglePremiumStatusAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = BuildController();

        // Act
        var result = await controller.TogglePremium(999);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>(
            because: "TogglePremium should return BadRequest when song is not found");
    }

    // ─── TC-SONG-007 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SONG-007: ToggleExplicit for a valid song returns JSON success.
    /// </summary>
    [Fact]
    public async Task ToggleExplicit_ValidSong_ReturnsJsonSuccess()
    {
        // Arrange
        _songServiceMock
            .Setup(s => s.ToggleExplicitStatusAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = BuildController();

        // Act
        var result = await controller.ToggleExplicit(1);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();

        var successProp = value!.GetType().GetProperty("success");
        successProp.Should().NotBeNull(because: "JSON result should have a 'success' property");
        successProp!.GetValue(value).Should().Be(true,
            because: "ToggleExplicit should return { success = true } when song is found");
    }

    // ─── TC-SONG-008 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SONG-008: SearchAlbums with a search term returns JSON results.
    /// </summary>
    [Fact]
    public async Task SearchAlbums_WithTerm_ReturnsJsonResults()
    {
        // Arrange
        var albums = new List<AlbumDto>
        {
            new() { AlbumId = 1, Title = "Greatest Hits" }
        };

        _albumServiceMock
            .Setup(s => s.SearchAlbumsAsync("great", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(albums);

        var controller = BuildController();

        // Act
        var result = await controller.SearchAlbums("great");

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value.Should().BeAssignableTo<IEnumerable<AlbumDto>>().Subject;
        value.Count().Should().Be(1,
            because: "SearchAlbums should return 1 album matching 'great'");
    }
}
