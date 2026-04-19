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
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;
using VibeMusic.Tests.Helpers;
using Xunit;

namespace VibeMusic.Tests.Admin;

/// <summary>
/// Unit tests for AdminArtistVerificationController.
/// Validates verify artist, verify batch, and reset verification functionality.
/// 
/// Note: SuccessResponse() in BaseController returns Ok(ApiResponse&lt;T&gt;),
/// so results are OkObjectResult with ApiResponse&lt;T&gt; as the value.
/// </summary>
public class AdminArtistVerificationControllerTests : BaseControllerTest<AdminArtistVerificationController>
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<IArtistVerificationService> _verificationServiceMock = new();
    private readonly Mock<IArtistService> _artistServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    private AdminArtistVerificationController BuildController()
    {
        var controller = new AdminArtistVerificationController(
            _verificationServiceMock.Object,
            _artistServiceMock.Object,
            _unitOfWorkMock.Object);

        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    /// <summary>
    /// Extracts the Data property from an ApiResponse returned by SuccessResponse().
    /// SuccessResponse returns Ok(ApiResponse&lt;T&gt;), so we unwrap OkObjectResult → ApiResponse → Data.
    /// </summary>
    private static object? GetResponseData(IActionResult result)
    {
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value;
        apiResponse.Should().NotBeNull();

        // ApiResponse<T> has a Data property
        var dataProp = apiResponse!.GetType().GetProperty("Data");
        dataProp.Should().NotBeNull(because: "ApiResponse should have a Data property");
        return dataProp!.GetValue(apiResponse);
    }

    // ─── TC-VERIFY-002 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-VERIFY-002: VerifyArtist with a successful match returns JSON with success=true and status="Verified".
    /// </summary>
    [Fact]
    public async Task VerifyArtist_Success_ReturnsJsonWithVerifiedStatus()
    {
        // Arrange
        var verificationResult = new ArtistVerificationResult
        {
            ArtistId = 1,
            ArtistName = "Taylor Swift",
            Status = ArtistVerificationStatus.Verified,
            DeezerArtistId = "12345",
            DeezerArtistName = "Taylor Swift",
            MatchScore = 0.95
        };

        _verificationServiceMock
            .Setup(s => s.VerifyArtistAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verificationResult);

        var controller = BuildController();

        // Act
        var result = await controller.VerifyArtist(1);

        // Assert
        var data = GetResponseData(result);
        data.Should().NotBeNull();

        var successProp = data!.GetType().GetProperty("success");
        successProp.Should().NotBeNull(because: "response data should have a 'success' property");
        successProp!.GetValue(data).Should().Be(true,
            because: "VerifyArtist should return success=true when status is Verified");

        var statusProp = data.GetType().GetProperty("status");
        statusProp.Should().NotBeNull(because: "response data should have a 'status' property");
        statusProp!.GetValue(data).Should().Be("Verified",
            because: "status should be 'Verified' when artist is successfully verified");
    }

    // ─── TC-VERIFY-003 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-VERIFY-003: VerifyArtist with no match returns JSON with success=false.
    /// </summary>
    [Fact]
    public async Task VerifyArtist_NotFound_ReturnsJsonWithUnverifiedStatus()
    {
        // Arrange
        var verificationResult = new ArtistVerificationResult
        {
            ArtistId = 1,
            ArtistName = "Unknown Artist",
            Status = ArtistVerificationStatus.Unverified,
            MatchScore = 0.0,
            FailureReason = "No match"
        };

        _verificationServiceMock
            .Setup(s => s.VerifyArtistAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verificationResult);

        var controller = BuildController();

        // Act
        var result = await controller.VerifyArtist(1);

        // Assert
        var data = GetResponseData(result);
        data.Should().NotBeNull();

        var successProp = data!.GetType().GetProperty("success");
        successProp.Should().NotBeNull(because: "response data should have a 'success' property");
        successProp!.GetValue(data).Should().Be(false,
            because: "VerifyArtist should return success=false when status is Unverified");
    }

    // ─── TC-VERIFY-004 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-VERIFY-004: VerifyBatch with valid batch size returns JSON with correct counts.
    /// 10 results: 5 Verified, 3 Unverified, 2 Failed → verifiedCount = 5.
    /// </summary>
    [Fact]
    public async Task VerifyBatch_ValidBatchSize_ReturnsJsonWithCounts()
    {
        // Arrange
        var results = new List<ArtistVerificationResult>();

        for (int i = 0; i < 5; i++)
            results.Add(new ArtistVerificationResult { ArtistId = i + 1, Status = ArtistVerificationStatus.Verified });

        for (int i = 5; i < 8; i++)
            results.Add(new ArtistVerificationResult { ArtistId = i + 1, Status = ArtistVerificationStatus.Unverified });

        for (int i = 8; i < 10; i++)
            results.Add(new ArtistVerificationResult { ArtistId = i + 1, Status = ArtistVerificationStatus.Failed });

        _verificationServiceMock
            .Setup(s => s.VerifyBatchAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var controller = BuildController();

        // Act
        var result = await controller.VerifyBatch(10);

        // Assert
        var data = GetResponseData(result);
        data.Should().NotBeNull();

        var verifiedCountProp = data!.GetType().GetProperty("verifiedCount");
        verifiedCountProp.Should().NotBeNull(because: "response data should have a 'verifiedCount' property");
        verifiedCountProp!.GetValue(data).Should().Be(5,
            because: "5 out of 10 results have status Verified");
    }

    // ─── TC-VERIFY-005 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-VERIFY-005: VerifyBatch with invalid batch size (> 100) returns BadRequest.
    /// </summary>
    [Fact]
    public async Task VerifyBatch_InvalidBatchSize_ReturnsBadRequest()
    {
        // Arrange
        var controller = BuildController();

        // Act
        var result = await controller.VerifyBatch(101);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>(
            because: "batch size > 100 should return HTTP 400 Bad Request");
    }

    // ─── TC-VERIFY-006 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-VERIFY-006: ResetVerification for a valid artist returns JSON with success=true.
    /// </summary>
    [Fact]
    public async Task ResetVerification_ValidArtist_ReturnsJsonSuccess()
    {
        // Arrange
        _verificationServiceMock
            .Setup(s => s.ResetVerificationAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.ResetVerification(1);

        // Assert
        var data = GetResponseData(result);
        data.Should().NotBeNull();

        var successProp = data!.GetType().GetProperty("success");
        successProp.Should().NotBeNull(because: "response data should have a 'success' property");
        successProp!.GetValue(data).Should().Be(true,
            because: "ResetVerification should return success=true on successful reset");
    }
}
