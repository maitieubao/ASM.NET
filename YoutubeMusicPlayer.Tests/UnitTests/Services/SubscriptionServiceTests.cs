using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using NUnit.Framework;
using Moq;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Domain.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace YoutubeMusicPlayer.Tests.UnitTests.Services;

[TestFixture]
public class SubscriptionServiceTests
{
    private Mock<IUnitOfWork> _mockUnitOfWork;
    private Mock<IGenericRepository<User>> _mockUserRepo;
    private Mock<IGenericRepository<Payment>> _mockPaymentRepo;
    private Mock<IGenericRepository<SubscriptionPlan>> _mockPlanRepo;
    private Mock<IGenericRepository<UserSubscription>> _mockUserSubRepo;
    private Mock<YoutubeMusicPlayer.Domain.Interfaces.IDbTransaction> _mockTransaction;
    private SubscriptionService _service;

    [SetUp]
    public void Setup()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockUserRepo = new Mock<IGenericRepository<User>>();
        _mockPaymentRepo = new Mock<IGenericRepository<Payment>>();
        _mockPlanRepo = new Mock<IGenericRepository<SubscriptionPlan>>();
        _mockUserSubRepo = new Mock<IGenericRepository<UserSubscription>>();
        _mockTransaction = new Mock<YoutubeMusicPlayer.Domain.Interfaces.IDbTransaction>();

        _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(_mockUserRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Payment>()).Returns(_mockPaymentRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<SubscriptionPlan>()).Returns(_mockPlanRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<UserSubscription>()).Returns(_mockUserSubRepo.Object);
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_mockTransaction.Object);

        _service = new SubscriptionService(_mockUnitOfWork.Object);
    }

    [Test]
    public async Task ProcessPaymentSuccessAsync_ValidPayment_UpgradesUserToPremium()
    {
        // Arrange
        long orderCode = 12345;
        string transactionId = "TXN_999";
        int userId = 10;
        int planId = 1;

        var payment = new Payment { OrderCode = orderCode, Status = "Pending", UserId = userId, PlanId = planId };
        var plan = new SubscriptionPlan { PlanId = planId, DurationDays = 30 };
        var user = new User { UserId = userId, IsPremium = false };

        _mockPaymentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Payment, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _mockPlanRepo.Setup(r => r.GetByIdAsync(planId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockUserSubRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<UserSubscription, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserSubscription?)null);

        // Act
        await _service.ProcessPaymentSuccessAsync(orderCode, transactionId, CancellationToken.None);

        // Assert
        Assert.That(payment.Status, Is.EqualTo("Success"));
        Assert.That(user.IsPremium, Is.True);
        _mockUserSubRepo.Verify(r => r.AddAsync(It.Is<UserSubscription>(s => s.UserId == userId && s.PlanId == planId && s.IsActive), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CancelSubscriptionAsync_ExistingSub_SetsToBeInactive()
    {
        // Arrange
        int userId = 5;
        var existingSub = new UserSubscription { UserId = userId, IsActive = true };

        _mockUserSubRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<UserSubscription, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(existingSub);

        // Act
        var result = await _service.CancelSubscriptionAsync(userId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(existingSub.IsActive, Is.False); // Downgraded
        _mockUserSubRepo.Verify(r => r.Update(existingSub), Times.Once);
        _mockUnitOfWork.Verify(u => u.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
