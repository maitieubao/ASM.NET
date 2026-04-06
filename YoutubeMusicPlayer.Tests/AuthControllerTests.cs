using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Routing;
using System;

namespace YoutubeMusicPlayer.Tests;

[TestFixture]
public class AuthControllerTests
{
    private AuthController _controller;
    private Mock<IAuthService> _mockAuthService;
    private Mock<IServiceProvider> _mockServiceProvider;
    private Mock<IAuthenticationService> _mockAuthenticationService;
    private Mock<IUrlHelperFactory> _mockUrlHelperFactory;
    private Mock<IUrlHelper> _mockUrlHelper;

    [SetUp]
    public void Setup()
    {
        _mockAuthService = new Mock<IAuthService>();
        _controller = new AuthController(_mockAuthService.Object);

        _mockAuthenticationService = new Mock<IAuthenticationService>();
        _mockUrlHelperFactory = new Mock<IUrlHelperFactory>();
        _mockUrlHelper = new Mock<IUrlHelper>();
        
        _mockUrlHelperFactory.Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>())).Returns(_mockUrlHelper.Object);

        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IAuthenticationService)))
            .Returns(_mockAuthenticationService.Object);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IUrlHelperFactory)))
            .Returns(_mockUrlHelperFactory.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = _mockServiceProvider.Object;

        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = httpContext
        };
        
        _controller.TempData = new Mock<ITempDataDictionary>().Object;
    }

    [TearDown]
    public void TearDown()
    {
        _controller?.Dispose();
    }

    [Test]
    public void Login_Get_ReturnsView()
    {
        var result = _controller.Login() as ViewResult;
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task Login_Post_InvalidModel_ReturnsView()
    {
        _controller.ModelState.AddModelError("Email", "Required");
        var model = new LoginDto();
        var result = await _controller.Login(model) as ViewResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.EqualTo(model));
    }

    [Test]
    public async Task Login_Post_ValidCredentials_RedirectsToHome()
    {
        // Arrange
        var model = new LoginDto { Email = "test@example.com", Password = "password123" };
        var user = new UserDto { UserId = 1, Username = "test", Email = "test@example.com", Role = "Customer" };
        
        _mockAuthService.Setup(s => s.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(user);

        _mockAuthenticationService.Setup(s => s.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Login(model) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Index"));
    }

    [Test]
    public async Task Register_Post_Success_RedirectsToHome()
    {
        // Arrange
        var model = new RegisterDto 
        { 
            Email = "new@example.com", 
            Password = "password123", 
            ConfirmPassword = "password123", 
            Username = "newmaster",
            DateOfBirth = DateTime.Now.AddYears(-20)
        };
        var user = new UserDto { UserId = 2, Username = "newmaster", Email = "new@example.com", Role = "Customer" };
        
        _mockAuthService.Setup(s => s.RegisterAsync(It.IsAny<RegisterDto>())).ReturnsAsync(user);

        _mockAuthenticationService.Setup(s => s.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Register(model) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Index"));
    }
}
