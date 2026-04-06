using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync()
    {
        // Optimized: Single SQL query using projection to avoid N+1 problem
        return await _unitOfWork.Repository<Category>().Query()
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                CategoryId = c.CategoryId,
                Name = c.Name,
                Description = c.Description,
                CreatedAt = c.CreatedAt,
                SongCount = _unitOfWork.Repository<Song>().Query().Count(s => s.CategoryId == c.CategoryId)
            })
            .ToListAsync();
    }

    public async Task<(IEnumerable<CategoryDto> Categories, int TotalCount)> GetPaginatedCategoriesAsync(int page, int pageSize)
    {
        var query = _unitOfWork.Repository<Category>().Query().AsNoTracking();
        
        int totalCount = await query.CountAsync();
        var categories = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoryDto
            {
                CategoryId = c.CategoryId,
                Name = c.Name,
                Description = c.Description,
                CreatedAt = c.CreatedAt,
                SongCount = _unitOfWork.Repository<Song>().Query().Count(s => s.CategoryId == c.CategoryId)
            })
            .ToListAsync();

        return (categories, totalCount);
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
    {
        // Optimized: Uses CountAsync instead of loading the entire song collection
        var c = await _unitOfWork.Repository<Category>().GetByIdAsync(id);
        if (c == null) return null;

        int songCount = await _unitOfWork.Repository<Song>().Query().CountAsync(s => s.CategoryId == id);
        
        return new CategoryDto
        {
            CategoryId = c.CategoryId,
            Name = c.Name,
            Description = c.Description,
            CreatedAt = c.CreatedAt,
            SongCount = songCount
        };
    }

    public async Task CreateCategoryAsync(CategoryDto dto)
    {
        // Validation: Ensure unique category name
        bool exists = await _unitOfWork.Repository<Category>().AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower());
        if (exists)
            throw new AppException($"Category with name '{dto.Name}' already exists.");

        var category = new Category
        {
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<Category>().AddAsync(category);
        await _unitOfWork.CompleteAsync();
    }

    public async Task UpdateCategoryAsync(CategoryDto dto)
    {
        // Validation: Unique name check for other records
        bool otherExists = await _unitOfWork.Repository<Category>().AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower() && c.CategoryId != dto.CategoryId);
        if (otherExists)
            throw new AppException($"Another category with name '{dto.Name}' already exists.");

        var category = await _unitOfWork.Repository<Category>().GetByIdAsync(dto.CategoryId);
        if (category != null)
        {
            category.Name = dto.Name;
            category.Description = dto.Description;
            _unitOfWork.Repository<Category>().Update(category);
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task DeleteCategoryAsync(int id)
    {
        // Integrity: Prevent deletion of category with associated songs
        bool hasSongs = await _unitOfWork.Repository<Song>().AnyAsync(s => s.CategoryId == id);
        if (hasSongs)
            throw new AppException("This category cannot be deleted because it contains songs. Please reassign or delete the songs first.");

        var category = await _unitOfWork.Repository<Category>().GetByIdAsync(id);
        if (category != null)
        {
            _unitOfWork.Repository<Category>().Remove(category);
            await _unitOfWork.CompleteAsync();
        }
    }
}
