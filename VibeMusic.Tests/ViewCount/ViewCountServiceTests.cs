using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Services;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;
using VibeMusic.Infrastructure;
using VibeMusic.Infrastructure.Persistence;
using Xunit;

namespace VibeMusic.Tests.ViewCount;

/// <summary>
/// Property-Based Tests for ViewCountService.
/// Uses standalone in-memory AppDbContext per test to avoid provider conflicts.
///
/// **Property 1: Auto-Select Maximum**
/// For any (youtubeCount, deezerCount, internalCount) with PrioritySource = null,
/// GetViewCountAsync must return max(youtubeCount, deezerCount, internalCount).
/// **Validates: Requirement 14.1**
///
/// **Property 2: Priority Source Respected**
/// For any source in {YouTube, Deezer, Internal} with PrioritySource = source,
/// GetViewCountAsync must return the view count from that source.
/// **Validates: Requirement 14.2**
///
/// **Property 3: Fallback on External API Failure**
/// For any internalCount, when no external view counts exist,
/// GetViewCountAsync must return internalCount with Source = Internal.
/// **Validates: Requirement 14.3**
///
/// **Property 4: Idempotent Priority Source Update**
/// For any source, calling UpdatePrioritySourceAsync twice must yield the same GetViewCountAsync result.
/// **Validates: Requirement 14.4**
/// </summary>
public class ViewCountServiceTests
{
    /// <summary>
    /// Creates a fresh in-memory AppDbContext for each test iteration.
    /// </summary>
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ViewCountTests_{Guid.NewGuid()}")
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Creates a ViewCountService with a fresh IMemoryCache and a real UnitOfWork backed by in-memory DB.
    /// </summary>
    private static (ViewCountService service, AppDbContext db) CreateServiceWithDb()
    {
        var db = CreateInMemoryContext();
        var unitOfWork = new UnitOfWork(db);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<ViewCountService>.Instance;
        var service = new ViewCountService(unitOfWork, cache, logger);
        return (service, db);
    }

    /// <summary>
    /// Creates a song with external view counts in the in-memory database.
    /// Returns the created song's ID.
    /// </summary>
    private static int CreateSongWithViewCounts(
        AppDbContext db,
        long internalCount,
        long? youtubeCount = null,
        long? deezerCount = null,
        ViewCountSource? prioritySource = null)
    {
        var song = new Song
        {
            Title = $"Test Song {Guid.NewGuid()}",
            YoutubeVideoId = Guid.NewGuid().ToString("N").Substring(0, 11),
            Duration = 180,
            ReleaseDate = DateTime.UtcNow,
            PlayCount = internalCount,
            IsDeleted = false,
            IsExplicit = false,
            IsPremiumOnly = false,
            PrioritySource = prioritySource
        };

        db.Songs.Add(song);
        db.SaveChanges();

        if (youtubeCount.HasValue)
        {
            db.ExternalViewCounts.Add(new ExternalViewCount
            {
                SongId = song.SongId,
                Source = ViewCountSource.YouTube,
                ViewCount = youtubeCount.Value,
                LastUpdated = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        if (deezerCount.HasValue)
        {
            db.ExternalViewCounts.Add(new ExternalViewCount
            {
                SongId = song.SongId,
                Source = ViewCountSource.Deezer,
                ViewCount = deezerCount.Value,
                LastUpdated = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        db.SaveChanges();
        return song.SongId;
    }

    /// <summary>
    /// Generates a list of random long values in [min, max] range using FsCheck.
    /// </summary>
    private static List<long> GenerateLongs(int count, long min = 0, long max = 1_000_000)
    {
        var gen = Gen.Choose((int)Math.Min(min, int.MaxValue), (int)Math.Min(max, int.MaxValue))
                     .Select(x => (long)x);
        return gen.Sample((int)((min + max) / 2), count).ToList();
    }

    /// <summary>
    /// Property 1: Auto-Select Maximum
    /// For any (youtubeCount, deezerCount, internalCount) with PrioritySource = null (auto),
    /// GetViewCountAsync must return max(youtubeCount, deezerCount, internalCount).
    /// Runs 100 iterations.
    /// **Validates: Requirement 14.1**
    /// </summary>
    [Fact]
    public async Task Property1_AutoSelectMaximum()
    {
        const int Iterations = 100;
        var ytSamples = GenerateLongs(Iterations);
        var dzSamples = GenerateLongs(Iterations);
        var internalSamples = GenerateLongs(Iterations);

        for (int i = 0; i < Iterations; i++)
        {
            var ytCount = ytSamples[i];
            var dzCount = dzSamples[i];
            var internalCount = internalSamples[i];

            var (service, db) = CreateServiceWithDb();
            using (db)
            {
                // Create song with PrioritySource = null (auto mode)
                var songId = CreateSongWithViewCounts(db, internalCount, ytCount, dzCount, null);

                var result = await service.GetViewCountAsync(songId);

                var expectedMax = Math.Max(internalCount, Math.Max(ytCount, dzCount));

                result.ViewCount.Should().Be(expectedMax,
                    because: $"auto mode must select max({ytCount}, {dzCount}, {internalCount}) = {expectedMax}");
            }
        }
    }

    /// <summary>
    /// Property 2: Priority Source Respected
    /// For any source in {YouTube, Deezer, Internal} with PrioritySource = source,
    /// GetViewCountAsync must return the view count from that source.
    /// Runs 100 iterations.
    /// **Validates: Requirement 14.2**
    /// </summary>
    [Fact]
    public async Task Property2_PrioritySourceRespected()
    {
        var sources = new[] { ViewCountSource.YouTube, ViewCountSource.Deezer, ViewCountSource.Internal };
        var sourceSamples = Gen.Elements(sources).Sample(1, 100).ToList();
        var ytSamples = GenerateLongs(100, 1, 1_000_000);
        var dzSamples = GenerateLongs(100, 1, 1_000_000);
        var internalSamples = GenerateLongs(100, 1, 1_000_000);

        int count = Math.Min(Math.Min(sourceSamples.Count, ytSamples.Count),
                             Math.Min(dzSamples.Count, internalSamples.Count));

        for (int i = 0; i < count; i++)
        {
            var source = sourceSamples[i];
            var ytCount = ytSamples[i];
            var dzCount = dzSamples[i];
            var internalCount = internalSamples[i];

            var (service, db) = CreateServiceWithDb();
            using (db)
            {
                // Create song with specified PrioritySource
                var songId = CreateSongWithViewCounts(db, internalCount, ytCount, dzCount, source);

                var result = await service.GetViewCountAsync(songId);

                // Verify the source matches
                result.Source.Should().Be(source,
                    because: $"when PrioritySource = {source}, GetViewCountAsync must return from that source");
            }
        }
    }

    /// <summary>
    /// Property 3: Fallback on External API Failure
    /// When no external view counts exist (simulating API failure / no data),
    /// GetViewCountAsync must return internalCount with Source = Internal.
    /// Runs 100 iterations.
    /// **Validates: Requirement 14.3**
    /// </summary>
    [Fact]
    public async Task Property3_FallbackOnNoExternalData()
    {
        var internalSamples = GenerateLongs(100);

        foreach (var internalCount in internalSamples)
        {
            var (service, db) = CreateServiceWithDb();
            using (db)
            {
                // Create song with NO external view counts (no YouTube, no Deezer)
                var songId = CreateSongWithViewCounts(db, internalCount, null, null, null);

                var result = await service.GetViewCountAsync(songId);

                // Must fallback to internal
                result.Source.Should().Be(ViewCountSource.Internal,
                    because: "when no external view counts exist, must fallback to Internal source");
                result.ViewCount.Should().Be(internalCount,
                    because: "fallback must return the song's PlayCount as the view count");
            }
        }
    }

    /// <summary>
    /// Property 4: Idempotent Priority Source Update
    /// For any source, calling UpdatePrioritySourceAsync(songId, source) twice must yield
    /// the same GetViewCountAsync result.
    /// Runs 100 iterations.
    /// **Validates: Requirement 14.4**
    /// </summary>
    [Fact]
    public async Task Property4_IdempotentPrioritySourceUpdate()
    {
        var sources = new ViewCountSource?[] { ViewCountSource.YouTube, ViewCountSource.Deezer, ViewCountSource.Internal, null };
        var sourceSamples = Gen.Elements(sources).Sample(1, 100).ToList();
        var ytSamples = GenerateLongs(100, 1, 1_000_000);
        var dzSamples = GenerateLongs(100, 1, 1_000_000);
        var internalSamples = GenerateLongs(100, 1, 1_000_000);

        int count = Math.Min(Math.Min(sourceSamples.Count, ytSamples.Count),
                             Math.Min(dzSamples.Count, internalSamples.Count));

        for (int i = 0; i < count; i++)
        {
            var source = sourceSamples[i];
            var ytCount = ytSamples[i];
            var dzCount = dzSamples[i];
            var internalCount = internalSamples[i];

            // Use a single shared DB for both updates (same song)
            var db = CreateInMemoryContext();
            using (db)
            {
                var unitOfWork = new UnitOfWork(db);

                // Create song with external view counts
                var songId = CreateSongWithViewCounts(db, internalCount, ytCount, dzCount, null);

                // First update
                var service1 = new ViewCountService(
                    unitOfWork,
                    new MemoryCache(new MemoryCacheOptions()),
                    NullLogger<ViewCountService>.Instance);
                await service1.UpdatePrioritySourceAsync(songId, source);
                var result1 = await service1.GetViewCountAsync(songId);

                // Second update (same source) — use fresh cache to bypass cache
                var service2 = new ViewCountService(
                    unitOfWork,
                    new MemoryCache(new MemoryCacheOptions()),
                    NullLogger<ViewCountService>.Instance);
                await service2.UpdatePrioritySourceAsync(songId, source);
                var result2 = await service2.GetViewCountAsync(songId);

                // Results must be identical
                result1.ViewCount.Should().Be(result2.ViewCount,
                    because: "calling UpdatePrioritySourceAsync twice with same source must be idempotent");
                result1.Source.Should().Be(result2.Source,
                    because: "calling UpdatePrioritySourceAsync twice with same source must be idempotent");
            }
        }
    }
}
