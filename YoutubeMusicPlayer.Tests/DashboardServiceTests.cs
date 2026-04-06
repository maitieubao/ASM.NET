using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
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
public class DashboardServiceTests
{
    private AppDbContext _context;
    private UnitOfWork _uow;
    private DashboardService _dashboardService;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _uow = new UnitOfWork(_context);
        _dashboardService = new DashboardService(_uow);
    }

    [TearDown]
    public void TearDown()
    {
        _uow?.Dispose();
        _context?.Dispose();
    }

    [Test]
    public async Task GetStatsAsync_ReturnsCorrectAggregates()
    {
        await _context.Users.AddAsync(new User { Email = "u1@test.com" });
        await _context.Songs.AddAsync(new Song { Title = "S1", PlayCount = 10 });
        await _context.SaveChangesAsync();

        var stats = await _dashboardService.GetStatsAsync();

        Assert.That(stats.TotalUsers, Is.EqualTo(1));
        Assert.That(stats.TotalSongs, Is.EqualTo(1));
    }

    [Test]
    public async Task ResolveReportAsync_TakeActionOnSong_RemovesSong()
    {
        var song = new Song { SongId = 1, Title = "Bad Song" };
        var report = new Report { ReportId = 1, TargetType = "Song", TargetId = "1", Status = "Pending" };
        await _context.Songs.AddAsync(song);
        await _context.Reports.AddAsync(report);
        await _context.SaveChangesAsync();

        await _dashboardService.ResolveReportAsync(1, true);

        Assert.That(await _context.Songs.AnyAsync(s => s.SongId == 1), Is.False);
        var updatedReport = await _context.Reports.FindAsync(1);
        Assert.That(updatedReport.Status, Is.EqualTo("Resolved"));
    }

    [Test]
    public async Task GetAllReportsAsync_ReturnsMappedDtos()
    {
        await _context.Reports.AddAsync(new Report { ReportId = 1, UserId = 1, TargetType = "Song", TargetId = "10", Reason = "Inappropriate" });
        await _context.SaveChangesAsync();

        var result = await _dashboardService.GetAllReportsAsync();
        Assert.That(result.Count(), Is.EqualTo(1));
        Assert.That(result.First().Reason, Is.EqualTo("Inappropriate"));
    }

    [Test]
    public async Task ResolveReportAsync_TakeActionOnUser_LocksUser()
    {
        var user = new User { UserId = 1, Username = "Bad Actor", IsLocked = false };
        var report = new Report { ReportId = 2, TargetType = "User", TargetId = "1", Status = "Pending" };
        await _context.Users.AddAsync(user);
        await _context.Reports.AddAsync(report);
        await _context.SaveChangesAsync();

        await _dashboardService.ResolveReportAsync(2, true);

        var updatedUser = await _context.Users.FindAsync(1);
        Assert.That(updatedUser.IsLocked, Is.True);
    }

    [Test]
    public async Task DismissReportAsync_UpdatesStatusToDismissed()
    {
        var report = new Report { ReportId = 3, Status = "Pending" };
        await _context.Reports.AddAsync(report);
        await _context.SaveChangesAsync();

        await _dashboardService.DismissReportAsync(3);

        var updatedReport = await _context.Reports.FindAsync(3);
        Assert.That(updatedReport.Status, Is.EqualTo("Dismissed"));
    }
}
