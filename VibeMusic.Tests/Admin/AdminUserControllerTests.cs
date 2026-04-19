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
/// Unit tests for AdminUserController.
/// Validates pagination, search, user details, lock/unlock, premium management,
/// soft delete, and self-protection invariants.
/// </summary>
public class AdminUserControllerTests : BaseControllerTest<AdminUserController>
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock = new();
    private readonly Mock<IInteractionService> _interactionServiceMock = new();
    private readonly Mock<ISongService> _songServiceMock = new();
    private readonly Mock<IPlaylistService> _playlistServiceMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the controller under test, wires up all mocks, sets the admin
    /// ControllerContext and TempData.
    /// </summary>
    private AdminUserController BuildController(int adminId = 1)
    {
        var controller = new AdminUserController(
            _userServiceMock.Object,
            _subscriptionServiceMock.Object,
            _interactionServiceMock.Object,
            _songServiceMock.Object,
            _playlistServiceMock.Object);

        controller.ControllerContext = TestAuthHelper.CreateAdminContext(adminId);
        SetupTempData(controller);
        return controller;
    }

    // ─── TC-USER-001 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-USER-001: Index with pagination returns correct page data.
    /// 25 total users, page 1 of 10 → 10 users returned, TotalPages = 3.
    /// </summary>
    [Fact]
    public async Task Index_Pagination_ReturnsCorrectPageData()
    {
        // Arrange
        var users = Enumerable.Range(1, 10)
            .Select(i => new UserDto { UserId = i, Username = $"user{i}" })
            .ToList();

        _userServiceMock
            .Setup(s => s.GetPaginatedUsersAsync(1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((users, 25));

        _subscriptionServiceMock
            .Setup(s => s.GetActivePlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SubscriptionPlanDto>());

        var controller = BuildController();

        // Act
        var result = await controller.Index(page: 1, pageSize: 10);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<VibeMusic.Models.Admin.AdminUserListViewModel>().Subject;

        model.TotalPages.Should().Be(3,
            because: "ceil(25 / 10) = 3 pages");
        model.Users.Count().Should().Be(10,
            because: "page 1 of 10 should return 10 users");
    }

    // ─── TC-USER-002 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-USER-002: Index with search term filters users correctly.
    /// </summary>
    [Fact]
    public async Task Index_Search_FiltersUsers()
    {
        // Arrange
        var aliceUsers = new List<UserDto>
        {
            new() { UserId = 1, Username = "alice" },
            new() { UserId = 2, Username = "alice_wonder" }
        };

        _userServiceMock
            .Setup(s => s.GetPaginatedUsersAsync(1, 10, "alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync((aliceUsers, 2));

        _subscriptionServiceMock
            .Setup(s => s.GetActivePlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SubscriptionPlanDto>());

        var controller = BuildController();

        // Act
        var result = await controller.Index(page: 1, pageSize: 10, searchTerm: "alice");

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<VibeMusic.Models.Admin.AdminUserListViewModel>().Subject;

        model.SearchTerm.Should().Be("alice",
            because: "the search term should be preserved in the view model");
        model.Users.Count().Should().Be(2,
            because: "only 2 users match the search term 'alice'");
    }

    // ─── TC-USER-003 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-USER-003: Details for a valid user returns view with full info
    /// (listening history, liked songs, playlists).
    /// </summary>
    [Fact]
    public async Task Details_ValidUser_ReturnsViewWithFullInfo()
    {
        // Arrange
        const int userId = 5;

        _userServiceMock
            .Setup(s => s.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto { UserId = userId, Username = "testuser" });

        var listeningHistory = Enumerable.Range(1, 3)
            .Select(i => new ListeningHistoryDto { HistoryId = i, UserId = userId, SongId = i })
            .ToList();

        _userServiceMock
            .Setup(s => s.GetUserListeningHistoryAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(listeningHistory);

        _interactionServiceMock
            .Setup(s => s.GetLikedSongIdsAsync(userId))
            .ReturnsAsync(new List<int> { 1, 2 });

        var likedSongs = new List<SongDto>
        {
            new() { SongId = 1, Title = "Song 1" },
            new() { SongId = 2, Title = "Song 2" }
        };

        _songServiceMock
            .Setup(s => s.GetSongsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(likedSongs);

        var playlists = new List<PlaylistDto>
        {
            new() { PlaylistId = 1, Title = "My Playlist", UserId = userId }
        };

        _playlistServiceMock
            .Setup(s => s.GetUserPlaylistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlists);

        var controller = BuildController();

        // Act
        var result = await controller.Details(userId);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;

        var history = viewResult.ViewData["ListeningHistory"] as IEnumerable<ListeningHistoryDto>;
        history.Should().NotBeNull();
        history!.Count().Should().Be(3,
            because: "user has 3 listening history records");

        var liked = viewResult.ViewData["LikedSongs"] as IEnumerable<SongDto>;
        liked.Should().NotBeNull();
        liked!.Count().Should().Be(2,
            because: "user has 2 liked songs");

        var userPlaylists = viewResult.ViewData["Playlists"] as IEnumerable<PlaylistDto>;
        userPlaylists.Should().NotBeNull();
        userPlaylists!.Count().Should().Be(1,
            because: "user has 1 playlist");
    }

    // ─── TC-USER-004 & 005 ───────────────────────────────────────────────────

    /// <summary>
    /// TC-USER-004 &amp; 005: ToggleUserLock for a valid (non-self) user updates
    /// lock status and sets TempData["Success"].
    /// </summary>
    [Fact]
    public async Task ToggleUserLock_ValidUser_UpdatesLockStatus()
    {
        // Arrange
        const int targetUserId = 2;

        _userServiceMock
            .Setup(s => s.ToggleUserLockAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = BuildController(adminId: 1);

        // Act
        var result = await controller.ToggleUserLock(targetUserId);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Details",
            because: "after toggling lock the controller should redirect to Details");

        AssertSuccess();
    }

    // ─── TC-USER-006 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-USER-006: ToggleUserLock on own account returns self-protection error
    /// and does NOT call ToggleUserLockAsync.
    /// </summary>
    [Fact]
    public async Task ToggleUserLock_SelfLock_ReturnsSelfProtectionError()
    {
        // Arrange – admin tries to lock themselves (adminId == userId)
        const int adminId = 1;
        var controller = BuildController(adminId: adminId);

        // Act
        var result = await controller.ToggleUserLock(adminId);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Details",
            because: "self-lock attempt should redirect back to Details");

        AssertError("You cannot lock your own administrative account.");

        _userServiceMock.Verify(
            s => s.ToggleUserLockAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ToggleUserLockAsync must NOT be called when admin tries to lock themselves");
    }

    // ─── TC-USER-007 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-USER-007: GrantPremium for a valid user grants premium successfully.
    /// </summary>
    [Fact]
    public async Task GrantPremium_ValidUser_GrantsSuccessfully()
    {
        // Arrange
        const int userId = 5;
        const int planId = 1;

        _userServiceMock
            .Setup(s => s.GrantPremiumByPlanAsync(userId, planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = BuildController();

        // Act
        var result = await controller.GrantPremium(id: userId, planId: planId);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after granting premium the controller should redirect to Index");

        AssertSuccess("Premium access granted successfully.");
    }

    // ─── TC-USER-008 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-USER-008: RevokePremium for a valid user revokes premium successfully.
    /// </summary>
    [Fact]
    public async Task RevokePremium_ValidUser_RevokesSuccessfully()
    {
        // Arrange
        const int userId = 5;

        _userServiceMock
            .Setup(s => s.RevokePremiumAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = BuildController();

        // Act
        var result = await controller.RevokePremium(id: userId);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after revoking premium the controller should redirect to Index");

        AssertSuccess("Premium access revoked successfully.");
    }

    // ─── TC-USER-009 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-USER-009: Delete a valid (non-self) user performs soft delete.
    /// </summary>
    [Fact]
    public async Task Delete_ValidUser_SoftDeletesUser()
    {
        // Arrange
        const int targetUserId = 5;

        _userServiceMock
            .Setup(s => s.DeleteUserAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = BuildController(adminId: 1);

        // Act
        var result = await controller.Delete(targetUserId);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after deleting a user the controller should redirect to Index");

        AssertSuccess("User marked as deleted.");
    }

    // ─── TC-USER-010 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-USER-010: Delete on own account returns self-protection error
    /// and does NOT call DeleteUserAsync.
    /// </summary>
    [Fact]
    public async Task Delete_SelfDelete_ReturnsSelfProtectionError()
    {
        // Arrange – admin tries to delete themselves
        const int adminId = 1;
        var controller = BuildController(adminId: adminId);

        // Act
        var result = await controller.Delete(adminId);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "self-delete attempt should redirect to Index");

        AssertError("You cannot delete your own administrative account.");

        _userServiceMock.Verify(
            s => s.DeleteUserAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "DeleteUserAsync must NOT be called when admin tries to delete themselves");
    }
}
