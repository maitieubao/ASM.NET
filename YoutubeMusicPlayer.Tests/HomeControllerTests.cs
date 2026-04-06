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

namespace YoutubeMusicPlayer.Tests;

[TestFixture]
public class HomeControllerTests
{
    private Mock<IYoutubeService> _mockYoutube;
    private Mock<ISongService> _mockSong;
    private Mock<IRecommendationService> _mockRec;
    private Mock<IInteractionService> _mockInteraction;
    private Mock<IBackgroundQueue> _mockQueue;
    private Mock<ISubscriptionService> _mockSub;
    private Mock<IArtistService> _mockArtist;
    private Mock<IGenreService> _mockGenre;
    private Mock<IAlbumService> _mockAlbum;
    private Mock<IDeezerService> _mockDeezer;
    private Mock<IAuthService> _mockAuth;

    private HomeController _homeController;

    [SetUp]
    public void Setup()
    {
        _mockYoutube = new Mock<IYoutubeService>();
        _mockSong = new Mock<ISongService>();
        _mockRec = new Mock<IRecommendationService>();
        _mockInteraction = new Mock<IInteractionService>();
        _mockQueue = new Mock<IBackgroundQueue>();
        _mockSub = new Mock<ISubscriptionService>();
        _mockArtist = new Mock<IArtistService>();
        _mockGenre = new Mock<IGenreService>();
        _mockAlbum = new Mock<IAlbumService>();
        _mockDeezer = new Mock<IDeezerService>();
        _mockAuth = new Mock<IAuthService>();

        _homeController = new HomeController(
            _mockYoutube.Object, _mockSong.Object, _mockRec.Object, _mockInteraction.Object,
            _mockQueue.Object, _mockSub.Object, _mockArtist.Object, _mockGenre.Object,
            _mockAlbum.Object, _mockDeezer.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testuser")
        }, "TestAuthentication"));

        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(IAuthService))).Returns(_mockAuth.Object);
        var mockTempDataFactory = new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>();
        mockServiceProvider.Setup(x => x.GetService(typeof(Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory))).Returns(mockTempDataFactory.Object);

        _homeController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user, RequestServices = mockServiceProvider.Object }
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
        _mockArtist.Setup(s => s.GetPaginatedArtistsAsync(1, 12, null))
            .ReturnsAsync((new List<ArtistDto>(), 0));
        _mockGenre.Setup(s => s.GetAllGenresAsync())
            .ReturnsAsync(new List<GenreDto>());

        var result = await _homeController.Index() as ViewResult;

        Assert.That(result, Is.Not.Null);
        var model = result.Model as YoutubeMusicPlayer.Models.HomeViewModel;
        Assert.That(model, Is.Not.Null);
    }

    [Test]
    public async Task GetStreamUrl_ReturnsPremiumRequired_IfSongIsPremiumOnly()
    {
        var song = new SongDto { SongId = 1, IsPremiumOnly = true };
        _mockSong.Setup(s => s.GetOrCreateByYoutubeIdAsync("vid123")).ReturnsAsync(song);
        _mockSub.Setup(s => s.IsUserPremiumAsync(1)).ReturnsAsync(false);

        var result = await _homeController.GetStreamUrl("https://www.youtube.com/watch?v=vid123") as JsonResult;

        Assert.That(result, Is.Not.Null);
        var data = result.Value;
        var errorValue = data.GetType().GetProperty("error")?.GetValue(data, null);
        Assert.That(errorValue, Is.EqualTo("PremiumRequired"));
    }

    [Test]
    public async Task Search_FiltersCompilationsFromMusicResults()
    {
        var results = new List<YoutubeVideoDetails>
        {
            new YoutubeVideoDetails { YoutubeVideoId = "s1", Title = "Song 1", Duration = TimeSpan.FromMinutes(3) },
            new YoutubeVideoDetails { YoutubeVideoId = "c1", Title = "Compilation", Duration = TimeSpan.FromMinutes(60) }
        };
        _mockYoutube.Setup(s => s.SearchVideosAsync("hits")).ReturnsAsync(results);
        _mockYoutube.Setup(s => s.IsMusic(It.IsAny<YoutubeVideoDetails>())).Returns((YoutubeVideoDetails v) => v.YoutubeVideoId == "s1");

        var result = await _homeController.Search("hits") as JsonResult;

        Assert.That(result, Is.Not.Null);
        var dataList = result.Value as IEnumerable<object>;
        Assert.That(dataList.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task GetHomeSection_ReturnsPartialViewForTrending()
    {
        var songs = new List<YoutubeVideoDetails> { new YoutubeVideoDetails { Title = "Trend" } };
        _mockYoutube.Setup(s => s.GetTrendingMusicAsync(15)).ReturnsAsync(songs);

        var result = await _homeController.GetHomeSection("trending") as PartialViewResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ViewName, Is.EqualTo("_HomeSection"));
        var model = result.Model as YoutubeMusicPlayer.Models.MusicSection;
        Assert.That(model.Title, Is.EqualTo("Thịnh hành hôm nay"));
    }

    [Test]
    public async Task Discovery_ReturnsPaginatedResults()
    {
        var songs = new List<YoutubeVideoDetails>();
        for(int i=0; i<30; i++) songs.Add(new YoutubeVideoDetails { Title = $"S{i}" });
        _mockRec.Setup(s => s.GetMoodMusicAsync("chill", 35, false)).ReturnsAsync(songs);

        var result = await _homeController.Discovery("chill", page: 1, json: true) as JsonResult;

        Assert.That(result, Is.Not.Null);
        var resList = result.Value as IEnumerable<YoutubeVideoDetails>;
        Assert.That(resList.Count(), Is.EqualTo(25));
    }
}
