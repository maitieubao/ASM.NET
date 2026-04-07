using System.Threading;
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

    public async Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync(CancellationToken ct = default)
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
            .ToListAsync(ct);
    }

    public async Task<(IEnumerable<CategoryDto> Categories, int TotalCount)> GetPaginatedCategoriesAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<Category>().Query().AsNoTracking();
        
        int totalCount = await query.CountAsync(ct);
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
            .ToListAsync(ct);

        return (categories, totalCount);
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(int id, CancellationToken ct = default)
    {
        // Optimized: Uses CountAsync instead of loading the entire song collection
        var c = await _unitOfWork.Repository<Category>().GetByIdAsync(id, ct);
        if (c == null) return null;

        int songCount = await _unitOfWork.Repository<Song>().Query().CountAsync(s => s.CategoryId == id, ct);
        
        return new CategoryDto
        {
            CategoryId = c.CategoryId,
            Name = c.Name,
            Description = c.Description,
            CreatedAt = c.CreatedAt,
            SongCount = songCount
        };
    }

    public async Task CreateCategoryAsync(CategoryDto dto, CancellationToken ct = default)
    {
        // Validation: Ensure unique category name
        bool exists = await _unitOfWork.Repository<Category>().AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower(), ct);
        if (exists)
            throw new AppException($"Category with name '{dto.Name}' already exists.");

        var category = new Category
        {
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<Category>().AddAsync(category, ct);
        await _unitOfWork.CompleteAsync(ct);
    }

    public async Task UpdateCategoryAsync(CategoryDto dto, CancellationToken ct = default)
    {
        // Validation: Unique name check for other records
        bool otherExists = await _unitOfWork.Repository<Category>().AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower() && c.CategoryId != dto.CategoryId, ct);
        if (otherExists)
            throw new AppException($"Another category with name '{dto.Name}' already exists.");

        var category = await _unitOfWork.Repository<Category>().GetByIdAsync(dto.CategoryId, ct);
        if (category != null)
        {
            category.Name = dto.Name;
            category.Description = dto.Description;
            _unitOfWork.Repository<Category>().Update(category);
            await _unitOfWork.CompleteAsync(ct);
        }
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        // Integrity: Prevent deletion of category with associated songs
        bool hasSongs = await _unitOfWork.Repository<Song>().AnyAsync(s => s.CategoryId == id, ct);
        if (hasSongs)
            throw new AppException("This category cannot be deleted because it contains songs. Please reassign or delete the songs first.");

        var category = await _unitOfWork.Repository<Category>().GetByIdAsync(id, ct);
        if (category != null)
        {
            _unitOfWork.Repository<Category>().Remove(category);
            await _unitOfWork.CompleteAsync(ct);
        }
    }
}
