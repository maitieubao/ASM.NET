using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using VibeMusic.Application.Services;
using VibeMusic.Domain.Entities;
using VibeMusic.Infrastructure;
using VibeMusic.Infrastructure.Persistence;

namespace VibeMusic.Tests;

/// <summary>
/// Bug Condition Exploration Test for ObjectDisposedException in Admin Premium Grant/Revoke
/// 
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
/// 
/// **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists.
/// This test encodes the expected behavior - it will validate the fix when it passes after implementation.
/// 
/// **GOAL**: Surface counterexamples that demonstrate the ObjectDisposedException occurs when admins grant or revoke premium.
/// 
/// The test uses scoped property-based testing approach with concrete failing cases:
/// - Admin grant premium (userId=1, planId=1)
/// - Admin grant premium (userId=2, planId=3)
/// - Admin revoke premium (userId=3 with active subscription)
/// </summary>
public class AdminPremiumGrantObjectDisposedBugTest : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserService _userService;

    public AdminPremiumGrantObjectDisposedBugTest()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
        _userService = new UserService(_unitOfWork);

        // Seed test data
        SeedTestData().GetAwaiter().GetResult();
    }

    private async Task SeedTestData()
    {
        // Create subscription plans
        var plans = new[]
        {
            new SubscriptionPlan { PlanId = 1, Name = "1 Month Premium", Price = 50000, DurationDays = 30, IsActive = true },
            new SubscriptionPlan { PlanId = 2, Name = "1 Year Premium", Price = 500000, DurationDays = 365, IsActive = true },
            new SubscriptionPlan { PlanId = 3, Name = "Lifetime Premium", Price = 2000000, DurationDays = 99999, IsActive = true }
        };

        await _context.SubscriptionPlans.AddRangeAsync(plans);

        // Create test users
        var users = new[]
        {
            new User { UserId = 1, Username = "testuser1", Email = "test1@example.com", Role = "Customer", IsPremium = false },
            new User { UserId = 2, Username = "testuser2", Email = "test2@example.com", Role = "Customer", IsPremium = false },
            new User { UserId = 3, Username = "testuser3", Email = "test3@example.com", Role = "Customer", IsPremium = true }
        };

        await _context.Users.AddRangeAsync(users);

        // Create active subscription for user 3
        var subscription = new UserSubscription
        {
            UserSubscriptionId = 1,
            UserId = 3,
            PlanId = 1,
            StartDate = DateTime.UtcNow.AddDays(-10),
            EndDate = DateTime.UtcNow.AddDays(20),
            IsActive = true
        };

        await _context.UserSubscriptions.AddAsync(subscription);

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Property 1: Bug Condition - Premium Grant/Revoke ObjectDisposedException
    /// 
    /// **EXPECTED OUTCOME ON UNFIXED CODE**: Test FAILS with ObjectDisposedException
    /// - Exception message should contain "Cannot access a disposed object" and "ManualResetEventSlim"
    /// - Exception should occur at GenericRepository.GetByIdAsync line 22
    /// 
    /// **EXPECTED OUTCOME AFTER FIX**: Test PASSES
    /// - Operations complete successfully without ObjectDisposedException
    /// - Transactions commit successfully
    /// - Database changes are persisted (user.IsPremium updated, UserSubscription records created/updated)
    /// </summary>
    [Fact]
    public async Task BugCondition_GrantPremium_1MonthPlan_ShouldCompleteWithoutObjectDisposedException()
    {
        // Arrange
        int userId = 1;
        int planId = 1;
        var ct = CancellationToken.None;

        // Act & Assert
        // On unfixed code: This should throw ObjectDisposedException
        // After fix: This should complete successfully
        var result = await _userService.GrantPremiumByPlanAsync(userId, planId, ct);

        // Verify expected behavior (after fix)
        Assert.True(result, "GrantPremiumByPlanAsync should return true for valid userId and planId");

        // Verify database changes were persisted
        var user = await _context.Users.FindAsync(userId);
        Assert.NotNull(user);
        Assert.True(user.IsPremium, "User.IsPremium should be true after granting premium");

        var subscription = await _context.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
        Assert.NotNull(subscription);
        Assert.Equal(planId, subscription.PlanId);
        Assert.True(subscription.IsActive, "UserSubscription should be active");
    }

    [Fact]
    public async Task BugCondition_GrantPremium_LifetimePlan_ShouldCompleteWithoutObjectDisposedException()
    {
        // Arrange
        int userId = 2;
        int planId = 3; // Lifetime plan
        var ct = CancellationToken.None;

        // Act & Assert
        // On unfixed code: This should throw ObjectDisposedException
        // After fix: This should complete successfully
        var result = await _userService.GrantPremiumByPlanAsync(userId, planId, ct);

        // Verify expected behavior (after fix)
        Assert.True(result, "GrantPremiumByPlanAsync should return true for valid userId and planId");

        // Verify database changes were persisted
        var user = await _context.Users.FindAsync(userId);
        Assert.NotNull(user);
        Assert.True(user.IsPremium, "User.IsPremium should be true after granting premium");

        var subscription = await _context.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
        Assert.NotNull(subscription);
        Assert.Equal(planId, subscription.PlanId);
        Assert.True(subscription.IsActive, "UserSubscription should be active");
        
        // Verify lifetime plan has far-future end date
        Assert.True(subscription.EndDate.Year == 9999, "Lifetime plan should have end date in year 9999");
    }

    [Fact]
    public async Task BugCondition_RevokePremium_WithActiveSubscription_ShouldCompleteWithoutObjectDisposedException()
    {
        // Arrange
        int userId = 3; // User with active subscription
        var ct = CancellationToken.None;

        // Act & Assert
        // On unfixed code: This should throw ObjectDisposedException
        // After fix: This should complete successfully
        var result = await _userService.RevokePremiumAsync(userId, ct);

        // Verify expected behavior (after fix)
        Assert.True(result, "RevokePremiumAsync should return true for valid userId");

        // Verify database changes were persisted
        var user = await _context.Users.FindAsync(userId);
        Assert.NotNull(user);
        Assert.False(user.IsPremium, "User.IsPremium should be false after revoking premium");

        var activeSubscriptions = await _context.UserSubscriptions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();
        Assert.Empty(activeSubscriptions);
    }

    [Fact]
    public async Task BugCondition_GrantPremium_InvalidUser_ShouldReturnFalseWithoutException()
    {
        // Arrange
        int invalidUserId = 999;
        int planId = 1;
        var ct = CancellationToken.None;

        // Act
        // This should return false gracefully without throwing ObjectDisposedException
        var result = await _userService.GrantPremiumByPlanAsync(invalidUserId, planId, ct);

        // Assert
        Assert.False(result, "GrantPremiumByPlanAsync should return false for invalid userId");
    }

    [Fact]
    public async Task BugCondition_GrantPremium_InvalidPlan_ShouldReturnFalseWithoutException()
    {
        // Arrange
        int userId = 1;
        int invalidPlanId = 999;
        var ct = CancellationToken.None;

        // Act
        // This should return false gracefully without throwing ObjectDisposedException
        var result = await _userService.GrantPremiumByPlanAsync(userId, invalidPlanId, ct);

        // Assert
        Assert.False(result, "GrantPremiumByPlanAsync should return false for invalid planId");
    }

    public void Dispose()
    {
        _context?.Dispose();
        _unitOfWork?.Dispose();
    }
}
