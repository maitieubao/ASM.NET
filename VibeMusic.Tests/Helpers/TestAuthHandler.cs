using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VibeMusic.Tests.Helpers;

/// <summary>
/// A test authentication handler that reads auth info from custom request headers.
/// Used by AdminWebApplicationFactory to simulate authenticated requests.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if test auth is enabled via header
        if (!Request.Headers.TryGetValue("X-Test-Auth-Enabled", out var enabledHeader)
            || enabledHeader.ToString() != "true")
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers.TryGetValue("X-Test-Auth-UserId", out var userIdHeader)
            ? userIdHeader.ToString()
            : "1";

        var role = Request.Headers.TryGetValue("X-Test-Auth-Role", out var roleHeader)
            ? roleHeader.ToString()
            : "Admin";

        var email = role == "Admin" ? "admin@test.com" : "user@test.com";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
