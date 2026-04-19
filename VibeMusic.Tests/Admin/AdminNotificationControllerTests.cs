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
/// Unit tests for AdminNotificationController.
/// Validates notification listing, sending (user/system), and deletion.
/// </summary>
public class AdminNotificationControllerTests : BaseControllerTest<AdminNotificationController>
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<INotificationService> _notificationServiceMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    private AdminNotificationController BuildController()
    {
        var controller = new AdminNotificationController(
            _notificationServiceMock.Object);

        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    // ─── TC-NOTIF-001 ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-NOTIF-001: Index returns view with notifications.
    /// </summary>
    [Fact]
    public async Task Index_ReturnsViewWithNotifications()
    {
        // Arrange
        var notifications = Enumerable.Range(1, 3)
            .Select(i => new NotificationDto { NotificationId = i, Title = $"Notification {i}" })
            .ToList();

        _notificationServiceMock
            .Setup(s => s.GetAllNotificationsAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        var controller = BuildController();

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<NotificationDto>>().Subject;
        model.Count().Should().Be(3,
            because: "Index should return all 3 notifications");
    }

    // ─── TC-NOTIF-002 ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-NOTIF-002: Send to specific user calls SendUserNotificationAsync and redirects.
    /// </summary>
    [Fact]
    public async Task Send_ToSpecificUser_SendsUserNotification()
    {
        // Arrange
        _notificationServiceMock
            .Setup(s => s.SendUserNotificationAsync(5, "Title", "Message", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();
        var request = new SendNotificationRequest
        {
            UserId = 5,
            Title = "Title",
            Message = "Message"
        };

        // Act
        var result = await controller.Send(request);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after sending a notification the controller should redirect to Index");

        AssertSuccess("Gửi thông báo thành công!");

        _notificationServiceMock.Verify(
            s => s.SendSystemNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "SendSystemNotificationAsync must NOT be called when sending to a specific user");
    }

    // ─── TC-NOTIF-003 ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-NOTIF-003: Send system notification (UserId=null) calls SendSystemNotificationAsync and redirects.
    /// </summary>
    [Fact]
    public async Task Send_SystemNotification_SendsSystemNotification()
    {
        // Arrange
        _notificationServiceMock
            .Setup(s => s.SendSystemNotificationAsync("Title", "Message", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();
        var request = new SendNotificationRequest
        {
            UserId = null,
            Title = "Title",
            Message = "Message"
        };

        // Act
        var result = await controller.Send(request);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after sending a system notification the controller should redirect to Index");

        AssertSuccess("Gửi thông báo thành công!");

        _notificationServiceMock.Verify(
            s => s.SendUserNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "SendUserNotificationAsync must NOT be called when sending a system notification");
    }

    // ─── TC-NOTIF-004 ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-NOTIF-004: Send with invalid model state sets error and redirects without calling any send method.
    /// </summary>
    [Fact]
    public async Task Send_InvalidModelState_SetsErrorAndRedirects()
    {
        // Arrange
        var controller = BuildController();
        controller.ModelState.AddModelError("Title", "Required");

        // Act
        var result = await controller.Send(new SendNotificationRequest());

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "invalid model state should redirect to Index");

        AssertError("Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.");

        _notificationServiceMock.Verify(
            s => s.SendUserNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "SendUserNotificationAsync must NOT be called when model state is invalid");

        _notificationServiceMock.Verify(
            s => s.SendSystemNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "SendSystemNotificationAsync must NOT be called when model state is invalid");
    }

    // ─── TC-NOTIF-005 ────────────────────────────────────────────────────────

    /// <summary>
    /// TC-NOTIF-005: Delete with valid ID deletes notification and redirects to Index.
    /// </summary>
    [Fact]
    public async Task Delete_ValidNotification_DeletesAndRedirects()
    {
        // Arrange
        _notificationServiceMock
            .Setup(s => s.DeleteNotificationAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.Delete(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after deleting a notification the controller should redirect to Index");

        AssertSuccess("Đã xóa thông báo.");
    }
}
