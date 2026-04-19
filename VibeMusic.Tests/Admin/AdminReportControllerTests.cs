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
using VibeMusic.Models.Admin;
using VibeMusic.Tests.Helpers;
using Xunit;

namespace VibeMusic.Tests.Admin;

/// <summary>
/// Unit tests for AdminReportController.
/// Validates report listing (filtered/all), details, resolve, and dismiss operations.
/// </summary>
public class AdminReportControllerTests : BaseControllerTest<AdminReportController>
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<IDashboardService> _dashboardServiceMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    private AdminReportController BuildController()
    {
        var controller = new AdminReportController(
            _dashboardServiceMock.Object);

        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    // ─── TC-REPORT-001 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-REPORT-001: Index with status filter returns filtered reports via paginated query.
    /// </summary>
    [Fact]
    public async Task Index_FilterByStatus_ReturnsFilteredReports()
    {
        // Arrange
        var reports = Enumerable.Range(1, 5)
            .Select(i => new ReportDto { ReportId = i, Status = "Pending", UserName = $"User{i}", TargetName = $"Song{i}", TargetType = "Song", Reason = "Spam" })
            .ToList();

        _dashboardServiceMock
            .Setup(s => s.GetPaginatedReportsAsync(1, 10, "Pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync((reports.AsEnumerable(), 5));

        var controller = BuildController();

        // Act
        var result = await controller.Index(page: 1, pageSize: 10, status: "Pending");

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<AdminReportListViewModel>().Subject;

        model.Reports.Count().Should().Be(5,
            because: "Index should return 5 filtered reports");
        model.Status.Should().Be("Pending",
            because: "the status filter should be preserved in the view model");
    }

    // ─── TC-REPORT-002 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-REPORT-002: Index with status="All" converts to null and returns all reports via GetAllReportsAsync.
    /// Note: The controller converts "All" to null before calling the service.
    /// </summary>
    [Fact]
    public async Task Index_FilterAll_ReturnsAllReports()
    {
        // Arrange
        var allReports = Enumerable.Range(1, 10)
            .Select(i => new ReportDto
            {
                ReportId = i,
                Status = i % 2 == 0 ? "Resolved" : "Pending",
                UserName = $"User{i}",
                TargetName = $"Song{i}",
                TargetType = "Song",
                Reason = "Spam"
            })
            .ToList();

        // When status="All", controller converts to null and calls GetPaginatedReportsAsync(1, 10, null)
        _dashboardServiceMock
            .Setup(s => s.GetPaginatedReportsAsync(1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((allReports.AsEnumerable(), 10));

        var controller = BuildController();

        // Act
        var result = await controller.Index(page: 1, pageSize: 10, status: "All");

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<AdminReportListViewModel>().Subject;

        model.Reports.Count().Should().Be(10,
            because: "Index with status='All' should return all 10 reports");
    }

    // ─── TC-REPORT-003 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-REPORT-003: Details with valid ID returns view with the report.
    /// </summary>
    [Fact]
    public async Task Details_ValidReport_ReturnsView()
    {
        // Arrange
        var report = new ReportDto
        {
            ReportId = 1,
            UserName = "TestUser",
            TargetName = "TestSong",
            TargetType = "Song",
            Reason = "Inappropriate content",
            Status = "Pending"
        };

        _dashboardServiceMock
            .Setup(s => s.GetReportByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var controller = BuildController();

        // Act
        var result = await controller.Details(1);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ReportDto>().Subject;

        model.ReportId.Should().Be(1,
            because: "Details should return the report with the requested ID");
    }

    // ─── TC-REPORT-004 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-REPORT-004: Details with non-existent ID returns NotFound.
    /// </summary>
    [Fact]
    public async Task Details_NotFound_ReturnsNotFound()
    {
        // Arrange
        _dashboardServiceMock
            .Setup(s => s.GetReportByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReportDto?)null);

        var controller = BuildController();

        // Act
        var result = await controller.Details(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>(
            because: "Details should return NotFound when the report does not exist");
    }

    // ─── TC-REPORT-005 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-REPORT-005: Resolve with valid ID resolves the report and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Resolve_ValidReport_ResolvesAndRedirects()
    {
        // Arrange
        _dashboardServiceMock
            .Setup(s => s.ResolveReportAsync(1, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.Resolve(1, takeAction: true);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after resolving a report the controller should redirect to Index");

        AssertSuccess("Report processed successfully.");
    }

    // ─── TC-REPORT-006 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-REPORT-006: Dismiss with valid ID dismisses the report and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Dismiss_ValidReport_DismissesAndRedirects()
    {
        // Arrange
        _dashboardServiceMock
            .Setup(s => s.DismissReportAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.Dismiss(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after dismissing a report the controller should redirect to Index");

        AssertSuccess("Report dismissed successfully.");
    }
}
