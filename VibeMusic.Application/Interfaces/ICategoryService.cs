using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync(CancellationToken ct = default);
    Task<CategoryDto?> GetCategoryByIdAsync(int id, CancellationToken ct = default);
    Task<(IEnumerable<CategoryDto> Categories, int TotalCount)> GetPaginatedCategoriesAsync(int page, int pageSize, CancellationToken ct = default);
    Task CreateCategoryAsync(CategoryDto categoryDto, CancellationToken ct = default);
    Task UpdateCategoryAsync(CategoryDto categoryDto, CancellationToken ct = default);
    Task DeleteCategoryAsync(int id, CancellationToken ct = default);
}
