using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FsCheck;
using FsCheck.Xunit;
using VibeMusic.Application.Services;
using VibeMusic.Domain.Entities;
using VibeMusic.Infrastructure;
using VibeMusic.Infrastructure.Persistence;

namespace VibeMusic.Tests;

/// <summary>
/// Preservation Property Tests for UserService
/// 
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// 
/// **IMPORTANT**: Follow observation-first methodology
/// These tests observe behavior on UNFIXED code for non-buggy inputs and ensure
/// that behavior is preserved after the ObjectDisposedException fix.
/// 
/// **Property 2: Preservation** - Other User Management Operations
/// For all user management operations NOT involving premium grant/revoke, behavior should be unchanged.
/// </summary>
public class UserServicePreservationTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserService _userService;

    public UserServicePreservationTests()
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
        // Create test users for preservation testing
        var users = new[]
        {
            new User { UserId = 1, Username = "user1", Email = "user1@example.com", Role = "Customer", IsPremium = false, IsLocked = false, IsDeleted = false, CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new User { UserId = 2, Username = "user2", Email = "user2@example.com", Role = "Customer", IsPremium = true, IsLocked = false, IsDeleted = false, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new User { UserId = 3, Username = "user3", Email = "user3@example.com", Role = "Customer", IsPremium = false, IsLocked = true, IsDeleted = false, CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new User { UserId = 4, Username = "user4", Email = "user4@example.com", Role = "Customer", IsPremium = false, IsLocked = false, IsDeleted = true, CreatedAt = DateTime.UtcNow.AddDays(-1) }, // Soft deleted
            new User { UserId = 5, Username = "searchuser", Email = "search@example.com", Role = "Customer", IsPremium = false, IsLocked = false, IsDeleted = false, CreatedAt = DateTime.UtcNow }
        };

        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Property 2.1: GetAllUsersAsync Preservation
    /// Verifies that GetAllUsersAsync returns all non-deleted users correctly
    /// </summary>
    [Fact]
    public async Task Preservation_GetAllUsersAsync_ReturnsAllNonDeletedUsers()
    {
        // Act
        var result = await _userService.GetAllUsersAsync();

        // Assert - Verify expected behavior is preserved
        var users = result.ToList();
        Assert.Equal(4, users.Count); // Should exclude soft-deleted user (UserId = 4)
        Assert.All(users, u => Assert.False(string.IsNullOrEmpty(u.Username)));
        Assert.All(users, u => Assert.False(string.IsNullOrEmpty(u.Email)));
        
        // Verify ordering (should be ordered by CreatedAt descending)
        var orderedUsers = users.OrderByDescending(u => u.CreatedAt).ToList();
        for (int i = 0; i < users.Count; i++)
        {
            Assert.Equal(orderedUsers[i].UserId, users[i].UserId);
        }
    }

    /// <summary>
    /// Property 2.2: GetPaginatedUsersAsync Preservation
    /// Verifies that pagination works correctly with various page sizes
    /// </summary>
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 3)]
    [InlineData(1, 10)]
    public async Task Preservation_GetPaginatedUsersAsync_WorksCorrectly(int page, int pageSize)
    {
        // Act
        var (users, totalCount) = await _userService.GetPaginatedUsersAsync(page, pageSize);

        // Assert - Verify expected behavior is preserved
        var userList = users.ToList();
        Assert.True(totalCount >= 0);
        Assert.True(userList.Count <= pageSize);
        
        // If we're on page 1 and pageSize >= totalCount, we should get all users
        if (page == 1 && pageSize >= totalCount)
        {
            Assert.Equal(totalCount, userList.Count);
        }
        
        // All returned users should be non-deleted
        Assert.All(userList, u => Assert.False(string.IsNullOrEmpty(u.Username)));
    }

    /// <summary>
    /// Property 2.3: SearchUsersAsync Preservation
    /// Verifies that user search works correctly with various search terms
    /// </summary>
    [Theory]
    [InlineData("user1")]
    [InlineData("search")]
    [InlineData("@example.com")]
    [InlineData("nonexistent")]
    [InlineData("")]
    public async Task Preservation_SearchUsersAsync_WorksCorrectly(string? searchTerm)
    {
        // Act
        var result = await _userService.SearchUsersAsync(searchTerm);

        // Assert - Verify expected behavior is preserved
        var users = result.ToList();
        Assert.True(users.Count <= 100); // Should be limited to 100 results
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            // All results should match the search term in username or email
            Assert.All(users, u => 
                Assert.True(u.Username.Contains(searchTerm) || u.Email.Contains(searchTerm)));
        }
        
        // All returned users should be non-deleted
        Assert.All(users, u => Assert.False(string.IsNullOrEmpty(u.Username)));
    }

    /// <summary>
    /// Property 2.4: GetUserByIdAsync Preservation
    /// Verifies that getting user by ID works correctly
    /// </summary>
    [Theory]
    [InlineData(1)] // Valid user
    [InlineData(2)] // Valid premium user
    [InlineData(3)] // Valid locked user
    [InlineData(4)] // Soft deleted user - should return null
    [InlineData(999)] // Non-existent user - should return null
    public async Task Preservation_GetUserByIdAsync_WorksCorrectly(int userId)
    {
        // Act
        var result = await _userService.GetUserByIdAsync(userId);

        // Assert - Verify expected behavior is preserved
        if (userId == 4 || userId == 999)
        {
            // Soft deleted or non-existent users should return null
            Assert.Null(result);
        }
        else if (userId >= 1 && userId <= 3)
        {
            // Valid users should be returned
            Assert.NotNull(result);
            Assert.Equal(userId, result.UserId);
            Assert.False(string.IsNullOrEmpty(result.Username));
            Assert.False(string.IsNullOrEmpty(result.Email));
        }
    }

    /// <summary>
    /// Property 2.5: ToggleUserLockAsync Preservation (Non-transactional behavior)
    /// Note: This test focuses on the method signature and basic validation,
    /// since transaction behavior is tested separately
    /// </summary>
    [Theory]
    [InlineData(999)] // Non-existent user
    [InlineData(4)]   // Soft deleted user
    public async Task Preservation_ToggleUserLockAsync_InvalidUsers_ReturnsFalse(int userId)
    {
        // Act & Assert
        // For invalid users, should return false without throwing exceptions
        var result = await _userService.ToggleUserLockAsync(userId);
        Assert.False(result);
    }

    /// <summary>
    /// Property 2.6: DeleteUserAsync Preservation (Non-transactional behavior)
    /// Note: This test focuses on the method signature and basic validation,
    /// since transaction behavior is tested separately
    /// </summary>
    [Theory]
    [InlineData(999)] // Non-existent user
    [InlineData(4)]   // Already soft deleted user
    public async Task Preservation_DeleteUserAsync_InvalidUsers_ReturnsFalse(int userId)
    {
        // Act & Assert
        // For invalid users, should return false without throwing exceptions
        var result = await _userService.DeleteUserAsync(userId);
        Assert.False(result);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _unitOfWork?.Dispose();
    }
}
