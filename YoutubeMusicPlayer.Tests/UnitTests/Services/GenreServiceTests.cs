using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace YoutubeMusicPlayer.Tests.UnitTests.Services;

[TestFixture]
public class GenreServiceTests
{
    private AppDbContext _context;
    private UnitOfWork _uow;
    private GenreService _genreService;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _uow = new UnitOfWork(_context);
        _genreService = new GenreService(_uow);
    }

    [TearDown]
    public void TearDown()
    {
        _uow?.Dispose();
        _context?.Dispose();
    }

    [Test]
    public async Task Genre_CRUD_WorksAsExpected()
    {
        var dto = new GenreDto { Name = "Pop", Description = "Pop music" };
        await _genreService.CreateGenreAsync(dto);
        var all = await _genreService.GetAllGenresAsync();
        Assert.That(all.Count(), Is.EqualTo(1));
        var genreId = all.First().GenreId;

        dto.GenreId = genreId;
        dto.Name = "Synth Pop";
        await _genreService.UpdateGenreAsync(dto);
        var updated = await _genreService.GetGenreByIdAsync(genreId);
        Assert.That(updated.Name, Is.EqualTo("Synth Pop"));

        await _genreService.DeleteGenreAsync(genreId);
        Assert.That(await _genreService.GetGenreByIdAsync(genreId), Is.Null);
    }
}

