using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync();
    Task<CategoryDto?> GetCategoryByIdAsync(int id);
    Task CreateCategoryAsync(CategoryDto categoryDto);
    Task UpdateCategoryAsync(CategoryDto categoryDto);
    Task DeleteCategoryAsync(int id);
}
