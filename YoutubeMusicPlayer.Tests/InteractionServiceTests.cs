using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace YoutubeMusicPlayer.Tests;

[TestFixture]
public class InteractionServiceTests
{
    private AppDbContext _context;
    private UnitOfWork _uow;
    private InteractionService _interactionService;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _uow = new UnitOfWork(_context);
        _interactionService = new InteractionService(_uow);
    }

    [TearDown]
    public void TearDown()
    {
        _uow?.Dispose();
        _context?.Dispose();
    }

    [Test]
    public async Task GetRecentListeningHistoryAsync_ReturnsSongIds()
    {
        await _context.ListeningHistories.AddAsync(new ListeningHistory { UserId = 1, SongId = 10, ListenedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await _interactionService.GetRecentListeningHistoryAsync(1);
        Assert.That(result.Contains(10), Is.True);
    }

    [Test]
    public async Task RecordSearchHistoryAsync_AddsToDb()
    {
        await _interactionService.RecordSearchHistoryAsync(1, "New Search");
        Assert.That(await _context.UserSearchHistories.AnyAsync(s => s.SearchQuery == "New Search"), Is.True);
    }

    [Test]
    public async Task ToggleLikeAsync_AddsThenRemoves()
    {
        var first = await _interactionService.ToggleLikeAsync(1, 100);
        Assert.That(first, Is.True);
        
        var second = await _interactionService.ToggleLikeAsync(1, 100);
        Assert.That(second, Is.False);
    }

    [Test]
    public async Task UpdateListeningStatsAsync_QualityGate_IncrementsPlayCount()
    {
        var song = new Song { SongId = 1, PlayCount = 0 };
        await _context.Songs.AddAsync(song);
        await _context.SaveChangesAsync();

        await _interactionService.UpdateListeningStatsAsync(1, 1, 61); // > 60s
        
        var updated = await _context.Songs.FindAsync(1);
        Assert.That(updated.PlayCount, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateListeningStatsAsync_ShortPlay_DoesNotIncrementPlayCount()
    {
        var song = new Song { SongId = 2, PlayCount = 0 };
        await _context.Songs.AddAsync(song);
        await _context.SaveChangesAsync();

        await _interactionService.UpdateListeningStatsAsync(1, 2, 30); // < 60s
        
        var updated = await _context.Songs.FindAsync(2);
        Assert.That(updated.PlayCount, Is.EqualTo(0));
    }
}
