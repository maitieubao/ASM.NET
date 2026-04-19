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
/// Unit tests for AdminSubscriptionController.
/// Validates plan listing, creation, and deletion (with/without active subscribers).
/// </summary>
public class AdminSubscriptionControllerTests : BaseControllerTest<AdminSubscriptionController>
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<ISubscriptionService> _subscriptionServiceMock = new();
    private readonly Mock<IDashboardService> _dashboardServiceMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    private AdminSubscriptionController BuildController()
    {
        var controller = new AdminSubscriptionController(
            _subscriptionServiceMock.Object,
            _dashboardServiceMock.Object);

        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    // ─── TC-SUB-001 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SUB-001: Index returns view with all plans.
    /// </summary>
    [Fact]
    public async Task Index_ReturnsViewWithPlans()
    {
        // Arrange
        var plans = Enumerable.Range(1, 3)
            .Select(i => new SubscriptionPlanDto { PlanId = i, Name = $"Plan {i}" })
            .ToList();

        _subscriptionServiceMock
            .Setup(s => s.GetAllPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);

        var controller = BuildController();

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<SubscriptionPlanDto>>().Subject;
        model.Count().Should().Be(3,
            because: "Index should return all 3 subscription plans");
    }

    // ─── TC-SUB-002 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SUB-002: Create with valid plan DTO creates the plan and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Create_ValidPlan_CreatesAndRedirects()
    {
        // Arrange
        _subscriptionServiceMock
            .Setup(s => s.CreatePlanAsync(It.IsAny<SubscriptionPlanDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();
        var dto = new SubscriptionPlanDto { Name = "Premium Monthly", Price = 99000 };

        // Act
        var result = await controller.Create(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after creating a plan the controller should redirect to Index");

        AssertSuccess("Gói hội viên đã được tạo thành công!");
    }

    // ─── TC-SUB-003 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SUB-003: Create with invalid model state sets error and redirects without calling CreatePlanAsync.
    /// </summary>
    [Fact]
    public async Task Create_InvalidModelState_SetsErrorAndRedirects()
    {
        // Arrange
        var controller = BuildController();
        controller.ModelState.AddModelError("Name", "Required");

        // Act
        var result = await controller.Create(new SubscriptionPlanDto());

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "invalid model state should redirect to Index");

        AssertError("Thông tin không hợp lệ. Vui lòng kiểm tra lại.");

        _subscriptionServiceMock.Verify(
            s => s.CreatePlanAsync(It.IsAny<SubscriptionPlanDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "CreatePlanAsync must NOT be called when model state is invalid");
    }

    // ─── TC-SUB-004 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SUB-004: Delete with no active subscribers sets simple success message.
    /// </summary>
    [Fact]
    public async Task Delete_NoActiveSubscribers_SetsSimpleSuccessMessage()
    {
        // Arrange
        _subscriptionServiceMock
            .Setup(s => s.GetActiveSubscriberCountAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _subscriptionServiceMock
            .Setup(s => s.DeletePlanAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.Delete(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after deleting a plan the controller should redirect to Index");

        AssertSuccess("Gói hội viên đã được ngưng hoạt động.");
    }

    // ─── TC-SUB-005 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-SUB-005: Delete with active subscribers sets warning message containing the count.
    /// </summary>
    [Fact]
    public async Task Delete_WithActiveSubscribers_SetsWarningMessage()
    {
        // Arrange
        _subscriptionServiceMock
            .Setup(s => s.GetActiveSubscriberCountAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _subscriptionServiceMock
            .Setup(s => s.DeletePlanAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.Delete(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after deleting a plan the controller should redirect to Index");

        TempDataStore.Should().ContainKey("Success",
            because: "controller should set TempData[\"Success\"] even when there are active subscribers");

        TempDataStore["Success"]!.ToString().Should().Contain("5",
            because: "the success message should mention the number of active subscribers");
    }
}
