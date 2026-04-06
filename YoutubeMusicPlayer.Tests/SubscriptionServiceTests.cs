using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace YoutubeMusicPlayer.Tests;

[TestFixture]
public class SubscriptionServiceTests
{
    private Mock<IUnitOfWork> _mockUow;
    private Mock<IGenericRepository<SubscriptionPlan>> _mockPlanRepo;
    private Mock<IGenericRepository<User>> _mockUserRepo;
    private Mock<IGenericRepository<UserSubscription>> _mockSubRepo;
    private Mock<IGenericRepository<Payment>> _mockPaymentRepo;
    private SubscriptionService _subService;

    [SetUp]
    public void Setup()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockPlanRepo = new Mock<IGenericRepository<SubscriptionPlan>>();
        _mockUserRepo = new Mock<IGenericRepository<User>>();
        _mockSubRepo = new Mock<IGenericRepository<UserSubscription>>();
        _mockPaymentRepo = new Mock<IGenericRepository<Payment>>();

        _mockUow.Setup(u => u.Repository<SubscriptionPlan>()).Returns(_mockPlanRepo.Object);
        _mockUow.Setup(u => u.Repository<User>()).Returns(_mockUserRepo.Object);
        _mockUow.Setup(u => u.Repository<UserSubscription>()).Returns(_mockSubRepo.Object);
        _mockUow.Setup(u => u.Repository<Payment>()).Returns(_mockPaymentRepo.Object);

        _subService = new SubscriptionService(_mockUow.Object);
    }

    [Test]
    public async Task GetActivePlansAsync_ShouldSeedAndReturnPlans()
    {
        _mockPlanRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<SubscriptionPlan> { new SubscriptionPlan { Name = "Basic" } });
        var result = await _subService.GetActivePlansAsync();
        Assert.That(result.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task IsUserPremiumAsync_ValidSubscription_ReturnsTrue()
    {
        var user = new User { UserId = 1, IsPremium = true };
        var sub = new UserSubscription { UserId = 1, IsActive = true, EndDate = DateTime.UtcNow.AddDays(1) };
        
        _mockUserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        _mockSubRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>())).ReturnsAsync(sub);

        var result = await _subService.IsUserPremiumAsync(1);
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task IsUserPremiumAsync_ExpiredSubscription_ReturnsFalseAndUpdatesUser()
    {
        var user = new User { UserId = 1, IsPremium = true };
        _mockUserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        _mockSubRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>())).ReturnsAsync((UserSubscription)null!);

        var result = await _subService.IsUserPremiumAsync(1);

        Assert.That(result, Is.False);
        Assert.That(user.IsPremium, Is.False);
        _mockUserRepo.Verify(r => r.Update(user), Times.Once);
        _mockUow.Verify(u => u.CompleteAsync(), Times.AtLeastOnce);
    }

    [Test]
    public async Task ProcessPaymentSuccessAsync_ShouldActivatePremiumAndExtendEndDate()
    {
        var payment = new Payment { OrderCode = 12345, UserId = 1, PlanId = 10, Status = "Pending" };
        var plan = new SubscriptionPlan { PlanId = 10, DurationDays = 30 };
        var user = new User { UserId = 1, IsPremium = false };
        
        _mockPaymentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Payment, bool>>>())).ReturnsAsync(payment);
        _mockPlanRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(plan);
        _mockUserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        _mockSubRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>())).ReturnsAsync((UserSubscription)null!);

        await _subService.ProcessPaymentSuccessAsync(12345, "TX_999");

        Assert.That(user.IsPremium, Is.True);
        Assert.That(payment.Status, Is.EqualTo("Success"));
        _mockSubRepo.Verify(r => r.AddAsync(It.IsAny<UserSubscription>()), Times.Once);
        _mockUow.Verify(u => u.CompleteAsync(), Times.AtLeastOnce);
    }
}
