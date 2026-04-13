using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;
using System;
using System.Linq;

namespace YoutubeMusicPlayer.Tests.UnitTests.Controllers;

[TestFixture]
public class HomeControllerTests
{
    private Mock<IHomeFacade> _mockHomeFacade;
    private Mock<IPlaybackFacade> _mockPlaybackFacade;
    private HomeController _homeController;

    [SetUp]
    public void Setup()
    {
        _mockHomeFacade = new Mock<IHomeFacade>();
        _mockPlaybackFacade = new Mock<IPlaybackFacade>();

        _homeController = new HomeController(_mockHomeFacade.Object, _mockPlaybackFacade.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testuser")
        }, "TestAuthentication"));

        _homeController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [TearDown]
    public void TearDown()
    {
        _homeController?.Dispose();
    }

    [Test]
    public async Task Index_ReturnsViewWithModel()
    {
        var mockViewModel = new YoutubeMusicPlayer.Application.DTOs.HomeViewModel();
        _mockHomeFacade.Setup(f => f.BuildHomeViewModelAsync(1)).ReturnsAsync(mockViewModel);

        var result = await _homeController.Index() as ViewResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.EqualTo(mockViewModel));
    }

    [Test]
    public async Task GetStreamUrl_ReturnsPremiumRequired_IfSongIsPremiumOnly()
    {
        var facadeResult = new PlaybackStreamDto { Error = "PremiumRequired", Message = "Premium required" };
        _mockPlaybackFacade.Setup(f => f.GetStreamAsync("vid123", null, null, 1)).ReturnsAsync(facadeResult);

        var rawResult = await _homeController.GetStreamUrl("vid123");
        Console.WriteLine("RAW RESULT IS: " + rawResult?.GetType().Name);
        var result = rawResult as Microsoft.AspNetCore.Mvc.ObjectResult;

        Assert.That(result, Is.Not.Null, "Result was cast to null. Actual type: " + rawResult?.GetType().Name);
        var data = result.Value as YoutubeMusicPlayer.Application.DTOs.ApiResponse<object>;
        Assert.That(data, Is.Not.Null);
        Assert.That(data.ErrorCode, Is.EqualTo("PremiumRequired"));
    }

    [Test]
    public async Task Search_ReturnsGlobalSearchResults()
    {
        var results = new List<SearchResultDto>
        {
            new SearchResultDto { VideoId = "s1", Title = "Song 1" }
        };
        _mockHomeFacade.Setup(f => f.SearchAllAsync("hits", 1)).ReturnsAsync(results);

        var result = await _homeController.Search("hits") as Microsoft.AspNetCore.Mvc.ObjectResult;

        Assert.That(result, Is.Not.Null);
        var dataList = result.Value as IEnumerable<SearchResultDto>;
        Assert.That(dataList.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task GetHomeSection_ReturnsPartialViewForTrending()
    {
        var section = new YoutubeMusicPlayer.Application.DTOs.MusicSection { Title = "Thịnh hành hôm nay" };
        _mockHomeFacade.Setup(f => f.GetHomeSectionAsync("trending", 1, false)).ReturnsAsync(section);

        var result = await _homeController.GetHomeSection("trending") as PartialViewResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ViewName, Is.EqualTo("_HomeSection"));
        var model = result.Model as YoutubeMusicPlayer.Application.DTOs.MusicSection;
        Assert.That(model.Title, Is.EqualTo("Thịnh hành hôm nay"));
    }
}

