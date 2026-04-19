using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Controllers;
using VibeMusic.Models.Admin;
using VibeMusic.Tests.Helpers;
using Xunit;

namespace VibeMusic.Tests.Admin;

/// <summary>
/// Unit tests for AdminTaxonomyController.
/// Validates genre and category CRUD operations.
/// </summary>
public class AdminTaxonomyControllerTests : BaseControllerTest<AdminTaxonomyController>
{
    // ─── Mocks ───────────────────────────────────────────────────────────────

    private readonly Mock<IGenreService> _genreServiceMock = new();
    private readonly Mock<ICategoryService> _categoryServiceMock = new();

    // ─── Helper ──────────────────────────────────────────────────────────────

    private AdminTaxonomyController BuildController()
    {
        var controller = new AdminTaxonomyController(
            _genreServiceMock.Object,
            _categoryServiceMock.Object);

        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        SetupTempData(controller);
        return controller;
    }

    // ─── TC-TAX-001 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-TAX-001: Index returns view with genres and categories.
    /// </summary>
    [Fact]
    public async Task Index_ReturnsViewWithGenresAndCategories()
    {
        // Arrange
        var genres = Enumerable.Range(1, 5)
            .Select(i => new GenreDto { GenreId = i, Name = $"Genre {i}" })
            .ToList();

        var categories = Enumerable.Range(1, 3)
            .Select(i => new CategoryDto { CategoryId = i, Name = $"Category {i}" })
            .ToList();

        _genreServiceMock
            .Setup(s => s.GetAllGenresAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(genres);

        _categoryServiceMock
            .Setup(s => s.GetAllCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        var controller = BuildController();

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<AdminTaxonomyViewModel>().Subject;

        model.Genres.Count().Should().Be(5,
            because: "Index should return all 5 genres");
        model.Categories.Count().Should().Be(3,
            because: "Index should return all 3 categories");
    }

    // ─── TC-TAX-002 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-TAX-002: CreateGenre with valid DTO creates genre and redirects to Index.
    /// </summary>
    [Fact]
    public async Task CreateGenre_ValidGenre_CreatesAndRedirects()
    {
        // Arrange
        _genreServiceMock
            .Setup(s => s.CreateGenreAsync(It.IsAny<GenreDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();
        var dto = new GenreDto { Name = "Rock" };

        // Act
        var result = await controller.CreateGenre(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after creating a genre the controller should redirect to Index");

        AssertSuccess("Thể loại đã được tạo thành công!");
    }

    // ─── TC-TAX-003 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-TAX-003: CreateGenre with invalid model state returns view without calling CreateGenreAsync.
    /// </summary>
    [Fact]
    public async Task CreateGenre_InvalidModelState_ReturnsViewWithErrors()
    {
        // Arrange
        var controller = BuildController();
        controller.ModelState.AddModelError("Name", "Required");

        // Act
        var result = await controller.CreateGenre(new GenreDto());

        // Assert
        result.Should().BeOfType<ViewResult>(
            because: "invalid model state should return the view, not redirect");

        _genreServiceMock.Verify(
            s => s.CreateGenreAsync(It.IsAny<GenreDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "CreateGenreAsync must NOT be called when model state is invalid");
    }

    // ─── TC-TAX-004 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-TAX-004: EditGenre with valid DTO updates genre and redirects to Index.
    /// </summary>
    [Fact]
    public async Task EditGenre_ValidGenre_UpdatesAndRedirects()
    {
        // Arrange
        _genreServiceMock
            .Setup(s => s.UpdateGenreAsync(It.IsAny<GenreDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();
        var dto = new GenreDto { GenreId = 1, Name = "Updated Rock" };

        // Act
        var result = await controller.EditGenre(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after updating a genre the controller should redirect to Index");

        AssertSuccess("Thể loại đã được cập nhật thành công!");
    }

    // ─── TC-TAX-005 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-TAX-005: DeleteGenre with valid ID deletes genre and redirects to Index.
    /// </summary>
    [Fact]
    public async Task DeleteGenre_ValidGenre_DeletesAndRedirects()
    {
        // Arrange
        _genreServiceMock
            .Setup(s => s.DeleteGenreAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.DeleteGenre(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after deleting a genre the controller should redirect to Index");

        AssertSuccess("Thể loại đã được xóa thành công!");
    }

    // ─── TC-TAX-006 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-TAX-006: CreateCategory with valid DTO creates category and redirects to Index.
    /// </summary>
    [Fact]
    public async Task CreateCategory_ValidCategory_CreatesAndRedirects()
    {
        // Arrange
        _categoryServiceMock
            .Setup(s => s.CreateCategoryAsync(It.IsAny<CategoryDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();
        var dto = new CategoryDto { Name = "Pop" };

        // Act
        var result = await controller.CreateCategory(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after creating a category the controller should redirect to Index");

        AssertSuccess("Danh mục đã được tạo thành công!");
    }

    // ─── TC-TAX-007 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-TAX-007: EditCategory with valid DTO updates category and redirects to Index.
    /// </summary>
    [Fact]
    public async Task EditCategory_ValidCategory_UpdatesAndRedirects()
    {
        // Arrange
        _categoryServiceMock
            .Setup(s => s.UpdateCategoryAsync(It.IsAny<CategoryDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();
        var dto = new CategoryDto { CategoryId = 1, Name = "Updated Pop" };

        // Act
        var result = await controller.EditCategory(dto);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after updating a category the controller should redirect to Index");

        AssertSuccess("Danh mục đã được cập nhật thành công!");
    }

    // ─── TC-TAX-008 ──────────────────────────────────────────────────────────

    /// <summary>
    /// TC-TAX-008: DeleteCategory with valid ID deletes category and redirects to Index.
    /// </summary>
    [Fact]
    public async Task DeleteCategory_ValidCategory_DeletesAndRedirects()
    {
        // Arrange
        _categoryServiceMock
            .Setup(s => s.DeleteCategoryAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = BuildController();

        // Act
        var result = await controller.DeleteCategory(1);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index",
            because: "after deleting a category the controller should redirect to Index");

        AssertSuccess("Danh mục đã được xóa thành công!");
    }
}
