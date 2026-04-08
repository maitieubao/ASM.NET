using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Tests.UnitTests.Controllers;

[TestFixture]
public class SongControllerTests
{
    private Mock<ISongService> _mockSong;
    private Mock<IAlbumService> _mockAlbum;
    private Mock<IGenreService> _mockGenre;
    private SongController _songController;

    [SetUp]
    public void Setup()
    {
        _mockSong = new Mock<ISongService>();
        _mockAlbum = new Mock<IAlbumService>();
        _mockGenre = new Mock<IGenreService>();
        _songController = new SongController(_mockSong.Object, _mockAlbum.Object, _mockGenre.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _songController?.Dispose();
    }

    [Test]
    public async Task Import_RedirectsToIndex()
    {
        _songController.TempData = new Mock<ITempDataDictionary>().Object;
        var result = await _songController.Import("https://youtube.com/watch?v=123") as RedirectToActionResult;
        Assert.That(result.ActionName, Is.EqualTo("Index"));
        _mockSong.Verify(s => s.ImportFromYoutubeAsync("https://youtube.com/watch?v=123"), Times.Once);
    }
}

