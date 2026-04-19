using System.Net;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using VibeMusic.Tests.Helpers;

namespace VibeMusic.Tests.Admin;

/// <summary>
/// Property-Based Tests for Authorization Enforcement across all admin endpoints.
///
/// **Property 1: Authorization Enforcement**
/// For any non-empty subset of admin endpoints, unauthenticated and non-admin (Customer role)
/// requests must NEVER return HTTP 200 OK. They must always be redirected or receive 403 Forbidden.
///
/// **Validates: Requirements 1.1, 1.2**
/// </summary>
public class AuthorizationEnforcementPropertyTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    private static readonly string[] AllAdminEndpoints =
    [
        "/Admin",
        "/AdminUser/Index",
        "/AdminSong/Index",
        "/AdminAlbum/Index",
        "/AdminArtist/Index",
        "/Admin/ArtistVerification",
        "/AdminPlaylist/Index",
        "/AdminSubscription/Index",
        "/AdminTaxonomy/Index",
        "/AdminNotification/Index",
        "/AdminReport/Index",
        "/AdminSupport/Comments",
        "/Admin/SongMetadata"
    ];

    public AuthorizationEnforcementPropertyTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Property 1a: Unauthenticated requests to any non-empty subset of admin endpoints
    /// must never return HTTP 200 OK. They should always redirect to login.
    ///
    /// Uses FsCheck to generate 100 random non-empty subsets of admin endpoints and
    /// verifies that unauthenticated HTTP requests to each endpoint never return 200 OK.
    ///
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Fact]
    public void Property1_UnauthenticatedRequests_NeverReturn200()
    {
        // Create the client ONCE outside the loop to avoid port exhaustion across 100 iterations
        var client = _factory.CreateUnauthenticatedClient();

        // Build a generator for non-empty subsets of admin endpoints
        var gen = Gen.SubListOf(AllAdminEndpoints)
                     .Where(list => list.Count > 0);

        // Run 100 iterations using FsCheck's generator
        var samples = gen.Sample(AllAdminEndpoints.Length, 100);

        foreach (var endpoints in samples)
        {
            foreach (var endpoint in endpoints)
            {
                var response = client.GetAsync(endpoint).GetAwaiter().GetResult();

                // Must NOT return 200 OK
                response.StatusCode.Should().NotBe(
                    HttpStatusCode.OK,
                    because: $"unauthenticated access to '{endpoint}' must not return 200 OK");

                // Must be a redirect (to login page)
                var isRedirect = response.StatusCode == HttpStatusCode.Redirect
                              || response.StatusCode == HttpStatusCode.Found
                              || response.StatusCode == HttpStatusCode.MovedPermanently;

                isRedirect.Should().BeTrue(
                    because: $"unauthenticated access to '{endpoint}' should redirect to login, but got {(int)response.StatusCode}");
            }
        }
    }

    /// <summary>
    /// Property 1b: Non-admin (Customer role) requests to any non-empty subset of admin endpoints
    /// must never return HTTP 200 OK. They should be redirected or receive 403 Forbidden.
    ///
    /// Uses FsCheck to generate 100 random non-empty subsets of admin endpoints and
    /// verifies that Customer-role HTTP requests to each endpoint never return 200 OK.
    ///
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Fact]
    public void Property1_NonAdminRequests_NeverReturn200()
    {
        // Create the client ONCE outside the loop to avoid port exhaustion across 100 iterations
        var client = _factory.CreateAuthenticatedClient(role: "Customer");

        // Build a generator for non-empty subsets of admin endpoints
        var gen = Gen.SubListOf(AllAdminEndpoints)
                     .Where(list => list.Count > 0);

        // Run 100 iterations using FsCheck's generator
        var samples = gen.Sample(AllAdminEndpoints.Length, 100);

        foreach (var endpoints in samples)
        {
            foreach (var endpoint in endpoints)
            {
                var response = client.GetAsync(endpoint).GetAwaiter().GetResult();

                // Must NOT return 200 OK
                response.StatusCode.Should().NotBe(
                    HttpStatusCode.OK,
                    because: $"Customer-role access to '{endpoint}' must not return 200 OK");

                // Must be a redirect or 403 Forbidden
                var isRedirect = response.StatusCode == HttpStatusCode.Redirect
                              || response.StatusCode == HttpStatusCode.Found
                              || response.StatusCode == HttpStatusCode.MovedPermanently;
                var isForbidden = response.StatusCode == HttpStatusCode.Forbidden;

                (isRedirect || isForbidden).Should().BeTrue(
                    because: $"Customer-role access to '{endpoint}' should redirect or return 403, but got {(int)response.StatusCode}");
            }
        }
    }
}
