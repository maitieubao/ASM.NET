using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PayOS.Models.Webhooks;
using Microsoft.AspNetCore.Mvc.Routing;
using System;

namespace YoutubeMusicPlayer.Tests;

[TestFixture]
public class PaymentControllerTests
{
    private Mock<IPayOSService> _mockPayOS;
    private Mock<ISubscriptionService> _mockSubService;
    private Mock<IAuthService> _mockAuth;
    private PaymentController _controller;

    [SetUp]
    public void Setup()
    {
        _mockPayOS = new Mock<IPayOSService>();
        _mockSubService = new Mock<ISubscriptionService>();
        _mockAuth = new Mock<IAuthService>();
        
        _controller = new PaymentController(_mockPayOS.Object, _mockSubService.Object, _mockAuth.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim("InternalUserId", "1"),
            new Claim(ClaimTypes.Email, "test@example.com")
        }, "mock"));

        var mockUrlHelper = new Mock<IUrlHelper>();
        mockUrlHelper.Setup(x => x.Action(It.IsAny<UrlActionContext>())).Returns("callback-url");
        _controller.Url = mockUrlHelper.Object;

        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = new DefaultHttpContext() { User = user }
        };
    }

    [TearDown]
    public void TearDown()
    {
        _controller?.Dispose();
    }

    [Test]
    public async Task CreatePayment_ValidPlan_ReturnsView()
    {
        _mockSubService.Setup(s => s.GetPlanByIdAsync(1)).ReturnsAsync(new SubscriptionPlanDto { PlanId = 1, Name = "Gold" });
        
        var result = await _controller.CreatePayment(1) as ViewResult;

        Assert.That(result, Is.Not.Null);
        var model = result.Model as SubscriptionPlanDto;
        Assert.That(model.Name, Is.EqualTo("Gold"));
    }

    [Test]
    public async Task CreatePaymentPost_ValidPlan_RedirectsToPayOS()
    {
        _mockSubService.Setup(s => s.GetPlanByIdAsync(1)).ReturnsAsync(new SubscriptionPlanDto { PlanId = 1, Name = "Gold", Price = 100000 });
        _mockPayOS.Setup(p => p.CreatePaymentLinkAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new PayOS.Models.V2.PaymentRequests.CreatePaymentLinkResponse { CheckoutUrl = "https://pay.os/checkout" });

        var result = await _controller.CreatePaymentPost(1) as RedirectResult;

        Assert.That(result.Url, Is.EqualTo("https://pay.os/checkout"));
    }

    [Test]
    public async Task Success_PaidStatus_ProcessesPayment()
    {
        var result = await _controller.Success(12345, "lnk_1", "PAID") as ViewResult;
        Assert.That(result, Is.Not.Null);
        _mockSubService.Verify(s => s.ProcessPaymentSuccessAsync(12345, "lnk_1"), Times.Once);
    }

    [Test]
    public async Task Webhook_ValidSignature_ReturnsOk()
    {
        var webhook = new Webhook { 
            Data = new WebhookData { OrderCode = 12345, PaymentLinkId = "lnk_1" } 
        };
        _mockPayOS.Setup(p => p.VerifyWebhookData(It.IsAny<Webhook>())).Returns(true);

        var result = await _controller.Webhook(webhook);

        Assert.That(result, Is.TypeOf<OkResult>());
        _mockSubService.Verify(s => s.ProcessPaymentSuccessAsync(12345, "lnk_1"), Times.Once);
    }

    [Test]
    public async Task Webhook_InvalidSignature_ReturnsBadRequest()
    {
        var webhook = new Webhook { };
        _mockPayOS.Setup(p => p.VerifyWebhookData(It.IsAny<Webhook>())).Returns(false);

        var result = await _controller.Webhook(webhook);

        Assert.That(result, Is.TypeOf<BadRequestResult>());
    }
}
