using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VibeMusic.Application.Interfaces;
using VibeMusic.Controllers;
using VibeMusic.Tests.Helpers;
using Xunit;

namespace VibeMusic.Tests.Admin;

/// <summary>
/// Unit tests for AdminSongMetadataController.
/// Validates enrich single song, enrich multiple songs, refresh outdated metadata,
/// and enrich artist songs functionality.
///
/// Note: AdminSongMetadataController inherits from BaseController.
/// SuccessResponse() returns Ok(ApiResponse&lt;T&gt;), so results are OkObjectResult
/// with ApiResponse&lt;T&gt; as the value. Use GetResponseData() to unwrap.
/// </summary>
public class AdminSongMetadataControllerTests : BaseControllerTest<AdminSongMetadataController>
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<ISongService> _songServiceMock = new();
    private readonly Mock<ISongMetadataEnrichmentService> _metadataEnrichmentServiceMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    private AdminSongMetadataController BuildController()
    {
        var controller = new AdminSongMetadataController(
            _songServiceMock.Object,
            _metadataEnrichmentServiceMock.Object);

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

        var dataProp = apiResponse!.GetType().GetProperty("Data");
        dataProp.Should().NotBeNull(because: "ApiResponse should have a Data property");
        return dataProp!.GetValue(apiResponse);
    }

    // ─── TC-METADATA-001 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-METADATA-001: EnrichSong with a song that exists on Deezer returns success=true.
    /// </summary>
    [Fact]
    public async Task EnrichSong_Success_ReturnsJsonSuccess()
    {
        // Arrange
        _songServiceMock
            .Setup(s => s.EnrichSongMetadataAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = BuildController();

        // Act
        var result = await controller.EnrichSong(1);

        // Assert
        var data = GetResponseData(result);
        data.Should().NotBeNull();

        var successProp = data!.GetType().GetProperty("success");
        successProp.Should().NotBeNull(because: "response data should have a 'success' property");
        successProp!.GetValue(data).Should().Be(true,
            because: "EnrichSong should return success=true when metadata enrichment succeeds");
    }

    // ─── TC-METADATA-002 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-METADATA-002: EnrichSong when song is not found on Deezer returns success=false.
    /// </summary>
    [Fact]
    public async Task EnrichSong_NotFound_ReturnsJsonFailure()
    {
        // Arrange
        _songServiceMock
            .Setup(s => s.EnrichSongMetadataAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = BuildController();

        // Act
        var result = await controller.EnrichSong(1);

        // Assert
        var data = GetResponseData(result);
        data.Should().NotBeNull();

        var successProp = data!.GetType().GetProperty("success");
        successProp.Should().NotBeNull(because: "response data should have a 'success' property");
        successProp!.GetValue(data).Should().Be(false,
            because: "EnrichSong should return success=false when song is not found on Deezer");
    }

    // ─── TC-METADATA-003 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-METADATA-003: EnrichMultiple with valid IDs returns enrichedCount and totalCount.
    /// 3 IDs provided, 3 enriched → enrichedCount=3, totalCount=3.
    /// </summary>
    [Fact]
    public async Task EnrichMultiple_ValidIds_ReturnsJsonWithCount()
    {
        // Arrange
        var songIds = new[] { 1, 2, 3 };

        _songServiceMock
            .Setup(s => s.EnrichMultipleSongsMetadataAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(songIds)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var controller = BuildController();

        // Act
        var result = await controller.EnrichMultiple(songIds);

        // Assert
        var data = GetResponseData(result);
        data.Should().NotBeNull();

        var enrichedCountProp = data!.GetType().GetProperty("enrichedCount");
        enrichedCountProp.Should().NotBeNull(because: "response data should have an 'enrichedCount' property");
        enrichedCountProp!.GetValue(data).Should().Be(3,
            because: "all 3 songs were successfully enriched");

        var totalCountProp = data.GetType().GetProperty("totalCount");
        totalCountProp.Should().NotBeNull(because: "response data should have a 'totalCount' property");
        totalCountProp!.GetValue(data).Should().Be(3,
            because: "3 song IDs were submitted");
    }

    // ─── TC-METADATA-004 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-METADATA-004: EnrichMultiple with more than 100 IDs returns BadRequest.
    /// </summary>
    [Fact]
    public async Task EnrichMultiple_TooManyIds_ReturnsBadRequest()
    {
        // Arrange
        var songIds = Enumerable.Range(1, 101).ToArray();
        var controller = BuildController();

        // Act
        var result = await controller.EnrichMultiple(songIds);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>(
            because: "submitting more than 100 song IDs should return HTTP 400 Bad Request");
    }

    // ─── TC-METADATA-005 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-METADATA-005: EnrichMultiple with an empty array returns BadRequest.
    /// </summary>
    [Fact]
    public async Task EnrichMultiple_EmptyArray_ReturnsBadRequest()
    {
        // Arrange
        var controller = BuildController();

        // Act
        var result = await controller.EnrichMultiple(Array.Empty<int>());

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>(
            because: "submitting an empty song ID array should return HTTP 400 Bad Request");
    }

    // ─── TC-METADATA-006 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-METADATA-006: RefreshOutdated with a valid batch size returns refreshedCount and batchSize.
    /// batchSize=50, 25 songs refreshed → refreshedCount=25, batchSize=50.
    /// </summary>
    [Fact]
    public async Task RefreshOutdated_ValidBatchSize_ReturnsJsonWithCount()
    {
        // Arrange
        _songServiceMock
            .Setup(s => s.RefreshOutdatedMetadataAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        var controller = BuildController();

        // Act
        var result = await controller.RefreshOutdated(50);

        // Assert
        var data = GetResponseData(result);
        data.Should().NotBeNull();

        var refreshedCountProp = data!.GetType().GetProperty("refreshedCount");
        refreshedCountProp.Should().NotBeNull(because: "response data should have a 'refreshedCount' property");
        refreshedCountProp!.GetValue(data).Should().Be(25,
            because: "25 songs were refreshed");

        var batchSizeProp = data.GetType().GetProperty("batchSize");
        batchSizeProp.Should().NotBeNull(because: "response data should have a 'batchSize' property");
        batchSizeProp!.GetValue(data).Should().Be(50,
            because: "the requested batch size was 50");
    }

    // ─── TC-METADATA-007 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-METADATA-007: RefreshOutdated with batchSize > 200 returns BadRequest.
    /// </summary>
    [Fact]
    public async Task RefreshOutdated_InvalidBatchSize_ReturnsBadRequest()
    {
        // Arrange
        var controller = BuildController();

        // Act
        var result = await controller.RefreshOutdated(201);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>(
            because: "batchSize > 200 should return HTTP 400 Bad Request");
    }

    // ─── TC-METADATA-008 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-METADATA-008: EnrichArtistSongs with a valid artist ID returns enrichedCount.
    /// artistId=1, 10 songs enriched → enrichedCount=10.
    /// </summary>
    [Fact]
    public async Task EnrichArtistSongs_ValidArtistId_ReturnsJsonWithCount()
    {
        // Arrange
        _metadataEnrichmentServiceMock
            .Setup(s => s.EnrichArtistSongsMetadataAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var controller = BuildController();

        // Act
        var result = await controller.EnrichArtistSongs(1);

        // Assert
        var data = GetResponseData(result);
        data.Should().NotBeNull();

        var enrichedCountProp = data!.GetType().GetProperty("enrichedCount");
        enrichedCountProp.Should().NotBeNull(because: "response data should have an 'enrichedCount' property");
        enrichedCountProp!.GetValue(data).Should().Be(10,
            because: "10 songs of the artist were enriched");
    }
}
