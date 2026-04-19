using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace VibeMusic.Tests.Helpers;

/// <summary>
/// Helper class for creating authentication contexts in unit tests.
/// </summary>
public static class TestAuthHelper
{
    /// <summary>
    /// Creates a ControllerContext with an Admin ClaimsPrincipal.
    /// </summary>
    public static ControllerContext CreateAdminContext(int adminId = 1)
    {
        var principal = CreateAdminPrincipal(adminId);
        return CreateControllerContext(principal);
    }

    /// <summary>
    /// Creates a ControllerContext with a Customer (regular user) ClaimsPrincipal.
    /// </summary>
    public static ControllerContext CreateUserContext(int userId = 10)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "user@test.com"),
            new Claim(ClaimTypes.Role, "Customer")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        return CreateControllerContext(principal);
    }

    /// <summary>
    /// Creates an Admin ClaimsPrincipal for use in integration tests.
    /// </summary>
    public static ClaimsPrincipal CreateAdminPrincipal(int adminId = 1)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Name, "admin@test.com"),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static ControllerContext CreateControllerContext(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }
}
