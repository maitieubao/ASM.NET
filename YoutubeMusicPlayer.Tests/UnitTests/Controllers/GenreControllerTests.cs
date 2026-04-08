using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using YoutubeMusicPlayer.Controllers;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Tests.UnitTests.Controllers;

[TestFixture]
public class GenreControllerTests
{
    private Mock<IGenreService> _mockGenre;
    private GenreController _genreController;

    [SetUp]
    public void Setup()
    {
        _mockGenre = new Mock<IGenreService>();
        _genreController = new GenreController(_mockGenre.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _genreController?.Dispose();
    }

    [Test]
    public async Task Index_ReturnsViewWithData()
    {
        _mockGenre.Setup(s => s.GetAllGenresAsync()).ReturnsAsync(new List<GenreDto>());
        var result = await _genreController.Index() as ViewResult;
        Assert.That(result, Is.Not.Null);
    }
}

