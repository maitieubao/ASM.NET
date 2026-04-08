using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace YoutubeMusicPlayer.Tests.UnitTests.Services;

[TestFixture]
public class NotificationServiceTests
{
    private AppDbContext _context;
    private UnitOfWork _uow;
    private NotificationService _notificationService;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _uow = new UnitOfWork(_context);
        _notificationService = new NotificationService(_uow);
    }

    [TearDown]
    public void TearDown()
    {
        _uow?.Dispose();
        _context?.Dispose();
    }

    [Test]
    public async Task Notification_System_WorksAsExpected()
    {
        await _notificationService.SendSystemNotificationAsync("Service Update", "New features");
        await _notificationService.SendUserNotificationAsync(1, "Welcome", "Thanks!");

        var userNotifs = await _notificationService.GetUserNotificationsAsync(1);
        Assert.That(userNotifs.Count(), Is.EqualTo(2));

        var id = userNotifs.First().NotificationId;
        await _notificationService.MarkAsReadAsync(id);
        var updated = await _notificationService.GetUserNotificationsAsync(1);
        Assert.That(updated.First(n => n.NotificationId == id).IsRead, Is.True);
    }
}

