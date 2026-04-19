using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using VibeMusic.Tests.Helpers;

namespace VibeMusic.Tests.Admin;

/// <summary>
/// Integration tests for Authentication and Authorization across all 13 admin endpoints.
/// Validates Requirements 1.1, 1.2, 1.3, 1.4
/// </summary>
public class AuthenticationIntegrationTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public AuthenticationIntegrationTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -------------------------------------------------------------------------
    // TC-AUTH-001: Unauthenticated requests must be redirected (HTTP 302)
    // -------------------------------------------------------------------------

    /// <summary>
    /// TC-AUTH-001: Verifies that unauthenticated requests to all 13 admin endpoints
    /// receive an HTTP 302 redirect (not 200 OK).
    /// Validates: Requirement 1.2
    /// </summary>
    [Theory]
    [InlineData("/Admin")]
    [InlineData("/AdminUser/Index")]
    [InlineData("/AdminSong/Index")]
    [InlineData("/AdminAlbum/Index")]
    [InlineData("/AdminArtist/Index")]
    [InlineData("/Admin/ArtistVerification")]
    [InlineData("/AdminPlaylist/Index")]
    [InlineData("/AdminSubscription/Index")]
    [InlineData("/AdminTaxonomy/Index")]
    [InlineData("/AdminNotification/Index")]
    [InlineData("/AdminReport/Index")]
    [InlineData("/AdminSupport/Comments")]
    [InlineData("/Admin/SongMetadata")]
    public async Task TC_AUTH_001_UnauthenticatedRequest_RedirectsToLogin(string url)
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            because: $"unauthenticated access to '{url}' should redirect to login, not return 200 OK");

        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            because: $"unauthenticated access to '{url}' must not return 200 OK");
    }

    // -------------------------------------------------------------------------
    // TC-AUTH-002: Non-admin (Customer role) must be denied access
    // -------------------------------------------------------------------------

    /// <summary>
    /// TC-AUTH-002: Verifies that a user with the "Customer" role is denied access
    /// to all 13 admin endpoints (HTTP 302 redirect or 403 Forbidden, not 200 OK).
    /// Validates: Requirement 1.1
    /// </summary>
    [Theory]
    [InlineData("/Admin")]
    [InlineData("/AdminUser/Index")]
    [InlineData("/AdminSong/Index")]
    [InlineData("/AdminAlbum/Index")]
    [InlineData("/AdminArtist/Index")]
    [InlineData("/Admin/ArtistVerification")]
    [InlineData("/AdminPlaylist/Index")]
    [InlineData("/AdminSubscription/Index")]
    [InlineData("/AdminTaxonomy/Index")]
    [InlineData("/AdminNotification/Index")]
    [InlineData("/AdminReport/Index")]
    [InlineData("/AdminSupport/Comments")]
    [InlineData("/Admin/SongMetadata")]
    public async Task TC_AUTH_002_NonAdminUser_IsDeniedAccess(string url)
    {
        // Arrange – authenticated as Customer (not Admin)
        var client = _factory.CreateAuthenticatedClient(role: "Customer");

        // Act
        var response = await client.GetAsync(url);

        // Assert: must NOT return 200 OK
        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            because: $"a Customer-role user must not be granted access to admin endpoint '{url}'");

        // Must be either a redirect (to login/access-denied) or 403 Forbidden
        var isRedirect = response.StatusCode == HttpStatusCode.Redirect
                      || response.StatusCode == HttpStatusCode.Found
                      || response.StatusCode == HttpStatusCode.MovedPermanently;
        var isForbidden = response.StatusCode == HttpStatusCode.Forbidden;

        (isRedirect || isForbidden).Should().BeTrue(
            because: $"non-admin access to '{url}' should result in a redirect or 403 Forbidden, but got {(int)response.StatusCode}");
    }

    // -------------------------------------------------------------------------
    // TC-AUTH-003: Admin user can access endpoints successfully
    // -------------------------------------------------------------------------

    /// <summary>
    /// TC-AUTH-003: Verifies that a user with the "Admin" role can access
    /// key admin GET endpoints and receives HTTP 200 OK or a valid redirect.
    /// Validates: Requirement 1.3
    /// </summary>
    [Theory]
    [InlineData("/Admin")]
    [InlineData("/AdminUser/Index")]
    [InlineData("/AdminSong/Index")]
    public async Task TC_AUTH_003_AdminUser_CanAccessAdminEndpoints(string url)
    {
        // Arrange – authenticated as Admin
        var client = _factory.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync(url);

        // Assert: must be 200 OK or a redirect (e.g. after successful login flow)
        var isSuccess = response.StatusCode == HttpStatusCode.OK;
        var isRedirect = response.StatusCode == HttpStatusCode.Redirect
                      || response.StatusCode == HttpStatusCode.Found;

        (isSuccess || isRedirect).Should().BeTrue(
            because: $"an Admin user should be able to access '{url}', but got {(int)response.StatusCode}");

        // Must NOT be 401 Unauthorized or 403 Forbidden
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            because: $"Admin user should not receive 401 for '{url}'");
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            because: $"Admin user should not receive 403 for '{url}'");
    }
}
