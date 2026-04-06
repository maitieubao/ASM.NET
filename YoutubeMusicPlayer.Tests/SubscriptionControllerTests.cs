using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Tests;

[TestFixture]
public class SubscriptionControllerTests
{
    private Mock<ISubscriptionService> _mockSub;
    private SubscriptionController _subController;

    [SetUp]
    public void Setup()
    {
        _mockSub = new Mock<ISubscriptionService>();
        _subController = new SubscriptionController(_mockSub.Object);
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { 
            new Claim(ClaimTypes.NameIdentifier, "1") 
        }, "mock"));
        _subController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
    }

    [TearDown]
    public void TearDown()
    {
        _subController?.Dispose();
    }

    [Test]
    public async Task Index_ReturnsViewWithPlans()
    {
        _mockSub.Setup(s => s.GetActivePlansAsync()).ReturnsAsync(new List<SubscriptionPlanDto>());
        var result = await _subController.Index() as ViewResult;
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task Download_ReturnsForbid_IfNotPremium()
    {
        _mockSub.Setup(s => s.IsUserPremiumAsync(1)).ReturnsAsync(false);
        var result = await _subController.Download("vid123", "Song Title");
        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }
}
