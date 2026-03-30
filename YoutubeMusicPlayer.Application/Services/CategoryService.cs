using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        var categories = await _unitOfWork.Repository<Category>().GetAllAsync();
        var result = new List<CategoryDto>();
        
        foreach (var c in categories)
        {
            var songs = await _unitOfWork.Repository<Song>().FindAsync(s => s.CategoryId == c.CategoryId);
            result.Add(new CategoryDto
            {
                CategoryId = c.CategoryId,
                Name = c.Name,
                Description = c.Description,
                CreatedAt = c.CreatedAt,
                SongCount = songs.Count()
            });
        }
        return result;
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
    {
        var c = await _unitOfWork.Repository<Category>().GetByIdAsync(id);
        if (c == null) return null;

        var songs = await _unitOfWork.Repository<Song>().FindAsync(s => s.CategoryId == c.CategoryId);
        return new CategoryDto
        {
            CategoryId = c.CategoryId,
            Name = c.Name,
            Description = c.Description,
            CreatedAt = c.CreatedAt,
            SongCount = songs.Count()
        };
    }

    public async Task CreateCategoryAsync(CategoryDto dto)
    {
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
        var category = await _unitOfWork.Repository<Category>().GetByIdAsync(id);
        if (category != null)
        {
            _unitOfWork.Repository<Category>().Remove(category);
            await _unitOfWork.CompleteAsync();
        }
    }
}
