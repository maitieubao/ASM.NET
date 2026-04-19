using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using VibeMusic.Infrastructure.Persistence;

namespace VibeMusic.Tests.Helpers;

/// <summary>
/// WebApplicationFactory for admin integration tests.
/// Replaces the real database with an in-memory database and configures test authentication.
/// </summary>
public class AdminWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use Testing environment to skip production-specific startup logic
        // (DB connection discovery, raw SQL index creation, etc.)
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove ALL DbContext-related registrations to ensure clean slate
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll(typeof(DbContextOptions));

            // Register in-memory database (unique name per factory instance)
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Disable HTTPS redirection in tests
            services.RemoveAll<Microsoft.AspNetCore.HttpsPolicy.HstsOptions>();

            // Add test authentication scheme
            services.AddAuthentication("TestScheme")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    "TestScheme", _ => { });
        });
    }

    /// <summary>
    /// After the host is built, ensure the in-memory database is created.
    /// This runs after ConfigureTestServices, so the InMemory provider is active.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure the in-memory database schema is created
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        return host;
    }

    /// <summary>
    /// Creates an HttpClient authenticated with the given role and userId.
    /// Uses cookie-based authentication via a special test header.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string role = "Admin", int userId = 1)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Pass auth info via custom headers that TestAuthHandler reads
        client.DefaultRequestHeaders.Add("X-Test-Auth-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Auth-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-Auth-Enabled", "true");

        return client;
    }

    /// <summary>
    /// Creates an HttpClient with no authentication and auto-redirect disabled.
    /// </summary>
    public HttpClient CreateUnauthenticatedClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    /// <summary>
    /// Gets a scoped AppDbContext for direct database manipulation in tests.
    /// Caller is responsible for disposing the scope.
    /// </summary>
    public AppDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}
