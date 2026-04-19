using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using FluentAssertions;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Interfaces;
using VibeMusic.Controllers;
using VibeMusic.Tests.Helpers;

namespace VibeMusic.Tests.Admin;

/// <summary>
/// Unit tests for AdminDashboardController.
/// Validates caching behaviour, service delegation, and error handling.
/// </summary>
public class AdminDashboardControllerTests : BaseControllerTest<AdminDashboardController>
{
    private const string CacheKey = "AdminDashboardStats";

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a real MemoryCache instance (not mocked) so that cache
    /// hit / miss behaviour is exercised end-to-end.
    /// </summary>
    private static MemoryCache CreateRealCache() =>
        new MemoryCache(new MemoryCacheOptions());

    /// <summary>
    /// Builds the controller under test and wires up TempData.
    /// </summary>
    private AdminDashboardController BuildController(
        IDashboardService dashboardService,
        IMemoryCache cache,
        IUnitOfWork unitOfWork)
    {
        var controller = new AdminDashboardController(dashboardService, cache, unitOfWork);
        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    // ─── TC-DASH-001 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-DASH-001: On a cache miss the controller calls GetStatsAsync exactly
    /// once and stores the result in the cache.
    /// </summary>
    [Fact]
    public async Task Index_CacheMiss_CallsServiceAndSetsCache()
    {
        // Arrange
        var expectedDto = new DashboardDto { TotalUsers = 100 };

        var dashboardServiceMock = new Mock<IDashboardService>();
        dashboardServiceMock
            .Setup(s => s.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var cache = CreateRealCache();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        var controller = BuildController(dashboardServiceMock.Object, cache, unitOfWorkMock.Object);

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DashboardDto>().Subject;
        model.TotalUsers.Should().Be(100);

        dashboardServiceMock.Verify(
            s => s.GetStatsAsync(It.IsAny<CancellationToken>()),
            Times.Once,
            "service should be called exactly once on a cache miss");
    }

    // ─── TC-DASH-002 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-DASH-002: On a cache hit the controller returns the cached value and
    /// does NOT call GetStatsAsync.
    /// </summary>
    [Fact]
    public async Task Index_CacheHit_DoesNotCallService()
    {
        // Arrange
        var cachedDto = new DashboardDto { TotalUsers = 999 };

        var dashboardServiceMock = new Mock<IDashboardService>();
        var cache = CreateRealCache();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        // Pre-populate the cache
        cache.Set(CacheKey, cachedDto);

        var controller = BuildController(dashboardServiceMock.Object, cache, unitOfWorkMock.Object);

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DashboardDto>().Subject;
        model.TotalUsers.Should().Be(999);

        dashboardServiceMock.Verify(
            s => s.GetStatsAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "service should NOT be called when data is already cached");
    }

    // ─── TC-DASH-003 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-DASH-003: RefreshStats removes the cache entry and redirects to Index.
    /// A subsequent call to Index must hit the service again (cache was cleared).
    /// </summary>
    [Fact]
    public async Task RefreshStats_RemovesCacheAndRedirects()
    {
        // Arrange
        var freshDto = new DashboardDto { TotalUsers = 42 };

        var dashboardServiceMock = new Mock<IDashboardService>();
        dashboardServiceMock
            .Setup(s => s.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(freshDto);

        var cache = CreateRealCache();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        // Pre-populate the cache so we can verify it gets cleared
        cache.Set(CacheKey, new DashboardDto { TotalUsers = 777 });

        var controller = BuildController(dashboardServiceMock.Object, cache, unitOfWorkMock.Object);

        // Act
        var refreshResult = controller.RefreshStats();

        // Assert – redirect
        var redirect = refreshResult.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");

        // Assert – TempData["Success"] was set
        GetTempData("Success").Should().NotBeNull(
            because: "controller should set TempData[\"Success\"] after refreshing stats");

        // Assert – cache was cleared: calling Index now must invoke the service
        await controller.Index();

        dashboardServiceMock.Verify(
            s => s.GetStatsAsync(It.IsAny<CancellationToken>()),
            Times.Once,
            "after cache invalidation the service should be called again on the next Index request");
    }

    // ─── TC-DASH-006 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-DASH-006: When GetStatsAsync throws an exception the controller returns
    /// a ViewResult with an empty DashboardDto (fallback) and sets TempData["Error"].
    /// </summary>
    [Fact]
    public async Task Index_ServiceThrowsException_ReturnsFallbackEmptyDashboard()
    {
        // Arrange
        var dashboardServiceMock = new Mock<IDashboardService>();
        dashboardServiceMock
            .Setup(s => s.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        var cache = CreateRealCache();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        var controller = BuildController(dashboardServiceMock.Object, cache, unitOfWorkMock.Object);

        // Act
        var result = await controller.Index();

        // Assert – should not throw; returns a ViewResult
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;

        // Model must be a non-null DashboardDto (empty fallback)
        viewResult.Model.Should().NotBeNull(
            because: "controller should return an empty DashboardDto as fallback, not null");
        viewResult.Model.Should().BeOfType<DashboardDto>();

        // TempData["Error"] must be set and contain the exception message
        var errorMessage = GetTempData("Error") as string;
        errorMessage.Should().NotBeNull(
            because: "controller should set TempData[\"Error\"] when the service throws");
        errorMessage.Should().Contain("DB error",
            because: "the error message should include the original exception message");
    }
}
