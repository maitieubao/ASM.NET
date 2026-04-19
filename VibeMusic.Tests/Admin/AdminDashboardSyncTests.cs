using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using FluentAssertions;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;
using VibeMusic.Infrastructure;
using VibeMusic.Infrastructure.Persistence;
using VibeMusic.Controllers;
using VibeMusic.Tests.Helpers;

namespace VibeMusic.Tests.Admin;

/// <summary>
/// Integration-style tests for AdminDashboardController sync / reset operations.
/// Uses an in-memory AppDbContext for LINQ-based operations and mocks for raw SQL.
/// </summary>
public class AdminDashboardSyncTests : BaseControllerTest<AdminDashboardController>
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh in-memory AppDbContext with a unique database name per test
    /// so tests are fully isolated from each other.
    /// </summary>
    private static AppDbContext CreateInMemoryContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>
    /// Builds the controller wired to a real UnitOfWork (backed by in-memory DB)
    /// and a real MemoryCache. TempData is set up via the base class helper.
    /// </summary>
    private AdminDashboardController BuildControllerWithRealUow(
        AppDbContext context,
        IDashboardService? dashboardService = null)
    {
        var uow = new UnitOfWork(context);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = dashboardService ?? Mock.Of<IDashboardService>();

        var controller = new AdminDashboardController(svc, cache, uow);
        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    /// <summary>
    /// Builds the controller wired to a mocked IUnitOfWork (for raw-SQL tests).
    /// </summary>
    private AdminDashboardController BuildControllerWithMockedUow(
        Mock<IUnitOfWork> unitOfWorkMock,
        IDashboardService? dashboardService = null)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = dashboardService ?? Mock.Of<IDashboardService>();

        var controller = new AdminDashboardController(svc, cache, unitOfWorkMock.Object);
        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    // ─── TC-DASH-004 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-DASH-004: SyncPlayCountFromHistory reads real ListeningHistory counts
    /// from the database and updates each Song's PlayCount accordingly.
    /// Songs with no history entries get PlayCount = 0.
    /// </summary>
    [Fact]
    public async Task SyncPlayCountFromHistory_UpdatesPlayCountFromListeningHistory()
    {
        // Arrange – seed in-memory database
        using var context = CreateInMemoryContext();

        // Song 1: has 5 listening history records
        context.Songs.Add(new Song { SongId = 1, Title = "Song One", PlayCount = 9999, YoutubeVideoId = "vid1" });

        // Song 2: has no listening history records
        context.Songs.Add(new Song { SongId = 2, Title = "Song Two", PlayCount = 100, YoutubeVideoId = "vid2" });

        // 5 ListeningHistory records for Song 1
        for (int i = 1; i <= 5; i++)
        {
            context.ListeningHistories.Add(new ListeningHistory
            {
                HistoryId = i,
                UserId = i,       // dummy user ids
                SongId = 1,
                ListenedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();

        var controller = BuildControllerWithRealUow(context);

        // Act
        var result = await controller.SyncPlayCountFromHistory();

        // Assert – redirect to Index
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");

        // Assert – Song 1 PlayCount updated to 5 (real listen count)
        var song1 = await context.Songs.FindAsync(1);
        song1.Should().NotBeNull();
        song1!.PlayCount.Should().Be(5,
            because: "Song 1 has 5 ListeningHistory records");

        // Assert – Song 2 PlayCount updated to 0 (no history)
        var song2 = await context.Songs.FindAsync(2);
        song2.Should().NotBeNull();
        song2!.PlayCount.Should().Be(0,
            because: "Song 2 has no ListeningHistory records");

        // Assert – TempData["Success"] contains the count of updated songs (2)
        var successMsg = GetTempData("Success") as string;
        successMsg.Should().NotBeNull();
        successMsg.Should().Contain("2",
            because: "both songs were updated (Song 1: 9999→5, Song 2: 100→0)");
    }

    // ─── TC-DASH-005 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC-DASH-005: ResetAllPlayCounts calls ExecuteSqlRawAsync with the correct
    /// raw SQL statement. Because InMemory does not support raw SQL, IUnitOfWork
    /// is mocked and we verify the call was made with the expected SQL.
    /// The soft-deleted song (is_deleted = true) is excluded by the WHERE clause
    /// in the SQL – this is verified by checking the SQL string passed to the mock.
    /// </summary>
    [Fact]
    public async Task ResetAllPlayCounts_CallsExecuteSqlRawWithCorrectSql()
    {
        // Arrange
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        // Simulate 3 non-deleted songs being reset
        unitOfWorkMock
            .Setup(u => u.ExecuteSqlRawAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(3);

        var controller = BuildControllerWithMockedUow(unitOfWorkMock);

        // Act
        var result = await controller.ResetAllPlayCounts();

        // Assert – redirect to Index
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");

        // Assert – ExecuteSqlRawAsync was called exactly once with SQL that
        // resets playcount and filters out soft-deleted rows
        unitOfWorkMock.Verify(
            u => u.ExecuteSqlRawAsync(
                It.Is<string>(sql =>
                    sql.Contains("playcount", StringComparison.OrdinalIgnoreCase) &&
                    sql.Contains("is_deleted", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()),
            Times.Once,
            "ResetAllPlayCounts should execute exactly one raw SQL statement targeting non-deleted songs");

        // Assert – TempData["Success"] was set (contains the affected row count)
        var successMsg = GetTempData("Success") as string;
        successMsg.Should().NotBeNull(
            because: "controller should set TempData[\"Success\"] after a successful reset");
        successMsg.Should().Contain("3",
            because: "the success message should include the number of affected rows returned by ExecuteSqlRawAsync");
    }
}
