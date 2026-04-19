using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using YoutubeMusicPlayer.Domain.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace YoutubeMusicPlayer.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        // 1. Ensure database is created and migrations are applied
        logger.LogInformation("[DbInitializer] Starting Database Migration. Connection: {Host}", context.Database.GetDbConnection().DataSource);
        
        int retryCount = 0;
        while (retryCount < 3)
        {
            try 
            {
                var connection = context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    logger.LogInformation("[DbInitializer] Manually opening connection (Attempt {Retry})...", retryCount + 1);
                    await connection.OpenAsync();
                }

                logger.LogInformation("[DbInitializer] Applying migrations...");
                context.Database.Migrate();
                logger.LogInformation("[DbInitializer] Migration successful.");
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                logger.LogWarning(ex, "[DbInitializer] Migration attempt {Retry} failed. Retrying in 2 seconds...", retryCount);
                if (retryCount >= 3)
                {
                    logger.LogCritical(ex, "[DbInitializer] FATAL: All migration attempts failed. Please check network connectivity or Supabase status.");
                    throw;
                }
                await Task.Delay(2000);
            }
        }

        // 2. Seed Admin User
        var seedConfig = configuration.GetSection("SeedData");
        var adminEmail = seedConfig["AdminEmail"] ?? "admin@musi.ca";
        var adminPassword = seedConfig["AdminPassword"] ?? "Admin1234!";

        if (!await context.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var admin = new User
            {
                Username = "MasterAdmin",
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(admin);
            Console.WriteLine($"[DbInitializer] Seeded Admin: {adminEmail}");
        }

        // 3. Seed Test User
        var testEmail = seedConfig["TestUserEmail"] ?? "minor@test.com";
        var testPassword = seedConfig["TestUserPassword"] ?? "Test1234!";

        if (!await context.Users.AnyAsync(u => u.Email == testEmail))
        {
            var minor = new User
            {
                Username = "YoungListener",
                Email = testEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(testPassword),
                Role = "Customer",
                CreatedAt = DateTime.UtcNow,
                DateOfBirth = new DateTime(2012, 1, 1).ToUniversalTime()
            };
            context.Users.Add(minor);
            Console.WriteLine($"[DbInitializer] Seeded Test User: {testEmail}");
        }

        await context.SaveChangesAsync();
    }
}
