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
/// Unit tests for AdminPlaylistController.
/// Validates pagination, song search, create featured playlist, add/remove songs, and delete.
/// 
/// Note: AdminPlaylistController uses CurrentAdminId from claims.
/// TestAuthHelper.CreateAdminContext(adminId: 1) sets NameIdentifier = "1".
/// </summary>
public class AdminPlaylistControllerTests
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<IPlaylistService> _playlistServiceMock = new();
    private readonly Mock<ISongService> _songServiceMock = new();
    private readonly Dictionary<string, object?> _tempDataStore = new();
    private readonly Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary> _tempDataMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    public AdminPlaylistControllerTests()
    {
        // Capture writes to TempData
        _tempDataMock
            .SetupSet(td => td[It.IsAny<string>()] = It.IsAny<object?>())
            .Callback<string, object?>((key, value) => _tempDataStore[key] = value);

        _tempDataMock
            .Setup(td => td[It.IsAny<string>()])
            .Returns<string>(key => _tempDataStore.TryGetValue(key, out var val) ? val : null);

        _tempDataMock
            .Setup(td => td.ContainsKey(It.IsAny<string>()))
            .Returns<string>(key => _tempDataStore.ContainsKey(key));
    }

    private AdminPlaylistController BuildController(int adminId = 1)
    {
        var controller = new AdminPlaylistController(
            _playlistServiceMock.Object,
            _songServiceMock.Object);

        controller.ControllerContext = TestAuthHelper.CreateAdminContext(adminId: adminId);
        controller.TempData = _tempDataMock.Object;
        return controller;
    }

    private object? GetTempData(string key)
        => _tempDataStore.TryGetValue(key, out var value) ? value : null;

    // ─── TC-PLAYLIST-001 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-PLAYLIST-001: Index with pagination returns correct page data.
    /// 15 total playlists, page 1 of 10 → 10 playlists returned, TotalPages = 2.
    /// </summary>
    [Fact]
    public async Task Index_Pagination_ReturnsCorrectPageData()
    {
        // Arrange
        var playlists = Enumerable.Range(1, 10)
            .Select(i => new PlaylistDto { PlaylistId = i, Title = $"Playlist {i}" })
            .ToList();

        _playlistServiceMock
            .Setup(s => s.GetPaginatedPlaylistsAsync(1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((playlists, 15));

        var controller = BuildController();

        // Act
        var result = await controller.Index(page: 1, pageSize: 10);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<VibeMusic.Models.Admin.AdminPlaylistListViewModel>().Subject;

        model.TotalPages.Should().Be(2,
            because: "ceil(15 / 10) = 2 pages");
        model.Playlists.Count().Should().Be(10,
            because: "page 1 of 10 should return 10 playlists");
    }

    // ─── TC-PLAYLIST-002 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-PLAYLIST-002: SearchSongs with a search term returns JSON results with id, text, thumbnail.
    /// </summary>
    [Fact]
    public async Task SearchSongs_WithTerm_ReturnsJsonResults()
    {
        // Arrange
        var songs = new List<SongDto>
        {
            new() { SongId = 1, Title = "Shape of You", AuthorName = "Ed Sheeran", ThumbnailUrl = "thumb1.jpg" },
            new() { SongId = 2, Title = "Shallow", AuthorName = "Lady Gaga", ThumbnailUrl = "thumb2.jpg" }
        };

        _songServiceMock
            .Setup(s => s.GetPaginatedSongsAsync(1, 20, "sha", It.IsAny<CancellationToken>()))
            .ReturnsAsync((songs, 2));

        var controller = BuildController();

        // Act
        var result = await controller.SearchSongs("sha");

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();

        // The result is an IEnumerable of anonymous objects with id, text, thumbnail
        var items = (value as System.Collections.IEnumerable)!.Cast<object>().ToList();
        items.Should().HaveCount(2,
            because: "SearchSongs should return 2 songs matching 'sha'");

        // Verify first item has id, text, thumbnail properties
        var firstItem = items[0];
        var idProp = firstItem.GetType().GetProperty("id");
        idProp.Should().NotBeNull(because: "each item should have an 'id' property");
        idProp!.GetValue(firstItem).Should().Be(1);

        var textProp = firstItem.GetType().GetProperty("text");
        textProp.Should().NotBeNull(because: "each item should have a 'text' property");

        var thumbnailProp = firstItem.GetType().GetProperty("thumbnail");
        thumbnailProp.Should().NotBeNull(because: "each item should have a 'thumbnail' property");
    }

    // ─── TC-PLAYLIST-002b ────────────────────────────────────────────────────

    /// <summary>
    /// TC-PLAYLIST-002b: SearchSongs with empty term returns empty array.
    /// </summary>
    [Fact]
    public async Task SearchSongs_EmptyTerm_ReturnsEmptyArray()
    {
        // Arrange
        var controller = BuildController();

        // Act
        var result = await controller.SearchSongs("");

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();

        var items = (value as System.Collections.IEnumerable)!.Cast<object>().ToList();
        items.Should().BeEmpty(
            because: "SearchSongs with empty term should return an empty array");

        // Verify GetPaginatedSongsAsync was never called
        _songServiceMock.Verify(
            s => s.GetPaginatedSongsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "GetPaginatedSongsAsync must NOT be called when search term is empty");
    }

    // ─── TC-PLAYLIST-003 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-PLAYLIST-003: CreateFeatured with valid playlist DTO creates the playlist and redirects to Index.
    /// </summary>
    [Fact]
    public async Task CreateFeatured_ValidPlaylist_CreatesAndRedirects()
    {
        // Arrange
        _playlistServiceMock
            .Setup(s => s.CreateFeaturedPlaylistAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaylistDto { PlaylistId = 1, Title = "Featured Playlist" });

        var controller = BuildController();
        var dto = new PlaylistDto { Title = "Featured Playlist", FeaturedType = "Top Hits" };

        // Act
        var result = await controller.CreateFeatured(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after creating a featured playlist the controller should redirect to Index");

        GetTempData("Success").Should().Be("Playlist đã được tạo!",
            because: "TempData[\"Success\"] should be set after successful creation");
    }

    // ─── TC-PLAYLIST-006 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-PLAYLIST-006: AddSong with valid IDs adds the song and redirects to EditFeatured.
    /// </summary>
    [Fact]
    public async Task AddSong_ValidIds_AddsAndRedirects()
    {
        // Arrange
        _playlistServiceMock
            .Setup(s => s.AddSongToPlaylistAsync(1, 5, 1, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController(adminId: 1);

        // Act
        var result = await controller.AddSong(playlistId: 1, songId: 5);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("EditFeatured",
            because: "after adding a song the controller should redirect to EditFeatured");

        GetTempData("Success").Should().Be("Đã thêm bài hát vào playlist!",
            because: "TempData[\"Success\"] should be set after successfully adding a song");
    }

    // ─── TC-PLAYLIST-007 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-PLAYLIST-007: RemoveSong with valid IDs removes the song and redirects to EditFeatured.
    /// </summary>
    [Fact]
    public async Task RemoveSong_ValidIds_RemovesAndRedirects()
    {
        // Arrange
        _playlistServiceMock
            .Setup(s => s.RemoveSongFromPlaylistAsync(1, 5, 1, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController(adminId: 1);

        // Act
        var result = await controller.RemoveSong(playlistId: 1, songId: 5);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("EditFeatured",
            because: "after removing a song the controller should redirect to EditFeatured");

        GetTempData("Success").Should().Be("Đã xóa bài hát khỏi playlist!",
            because: "TempData[\"Success\"] should be set after successfully removing a song");
    }

    // ─── TC-PLAYLIST-008 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-PLAYLIST-008: Delete with valid playlist ID deletes the playlist and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Delete_ValidPlaylist_DeletesAndRedirects()
    {
        // Arrange
        _playlistServiceMock
            .Setup(s => s.DeletePlaylistAsync(1, 1, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController(adminId: 1);

        // Act
        var result = await controller.Delete(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after deleting a playlist the controller should redirect to Index");

        GetTempData("Success").Should().Be("Đã xóa playlist!",
            because: "TempData[\"Success\"] should be set after successfully deleting a playlist");
    }
}
