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
public class CategoryServiceTests
{
    private AppDbContext _context;
    private UnitOfWork _uow;
    private CategoryService _categoryService;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _uow = new UnitOfWork(_context);
        _categoryService = new CategoryService(_uow);
    }

    [TearDown]
    public void TearDown()
    {
        _uow?.Dispose();
        _context?.Dispose();
    }

    [Test]
    public async Task Category_CRUD_WorksAsExpected()
    {
        var dto = new CategoryDto { Name = "Workout" };
        await _categoryService.CreateCategoryAsync(dto);
        var all = await _categoryService.GetAllCategoriesAsync();
        Assert.That(all.Count(), Is.EqualTo(1));
        var categoryId = all.First().CategoryId;

        dto.CategoryId = categoryId;
        dto.Name = "Intense Workout";
        await _categoryService.UpdateCategoryAsync(dto);
        var updated = await _categoryService.GetCategoryByIdAsync(categoryId);
        Assert.That(updated.Name, Is.EqualTo("Intense Workout"));

        await _categoryService.DeleteCategoryAsync(categoryId);
        Assert.That(await _categoryService.GetCategoryByIdAsync(categoryId), Is.Null);
    }
}

