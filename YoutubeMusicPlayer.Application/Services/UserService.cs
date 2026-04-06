using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        // RAM Protection: Use SQL projection so we don't load full User entities into memory
        return await _unitOfWork.Repository<User>().Query()
            .Where(u => !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserDto
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role,
                AvatarUrl = u.AvatarUrl,
                CreatedAt = u.CreatedAt,
                IsPremium = u.IsPremium,
                IsLocked = u.IsLocked,
                DateOfBirth = u.DateOfBirth
            })
            .ToListAsync(ct);
    }

    public async Task<(IEnumerable<UserDto> Users, int TotalCount)> GetPaginatedUsersAsync(int page, int pageSize, string? searchTerm = null, CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<User>().Query().Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u => u.Username.Contains(searchTerm) || u.Email.Contains(searchTerm));
        }

        int totalCount = await query.CountAsync(ct);
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role,
                AvatarUrl = u.AvatarUrl,
                CreatedAt = u.CreatedAt,
                IsPremium = u.IsPremium,
                IsLocked = u.IsLocked,
                DateOfBirth = u.DateOfBirth
            })
            .ToListAsync(ct);

        return (users, totalCount);
    }

    public async Task<UserDto?> GetUserByIdAsync(int id, CancellationToken ct = default)
    {
        var u = await _unitOfWork.Repository<User>().GetByIdAsync(id, ct);
        if (u == null || u.IsDeleted) return null;

        return MapToDto(u);
    }

    public async Task<bool> ToggleUserLockAsync(int id, CancellationToken ct = default)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(id, ct);
            if (user == null || user.IsDeleted) return false;

            user.IsLocked = !user.IsLocked;
            _unitOfWork.Repository<User>().Update(user);
            await _unitOfWork.CompleteAsync(ct);
            
            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> DeleteUserAsync(int id, CancellationToken ct = default)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(id, ct);
            if (user == null || user.IsDeleted) return false;

            // SOFT DELETE
            user.IsDeleted = true;
            _unitOfWork.Repository<User>().Update(user);
            await _unitOfWork.CompleteAsync(ct);
            
            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IEnumerable<UserDto>> SearchUsersAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) 
            return await GetPaginatedUsersAsync(1, 100, null, ct).ContinueWith(t => t.Result.Users);

        // Security & RAM protection: Limit results to 100 to prevent server strain
        return await _unitOfWork.Repository<User>().Query()
            .Where(u => !u.IsDeleted && (u.Username.Contains(query) || u.Email.Contains(query)))
            .OrderByDescending(u => u.CreatedAt)
            .Take(100)
            .Select(u => new UserDto
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role,
                AvatarUrl = u.AvatarUrl,
                CreatedAt = u.CreatedAt,
                IsPremium = u.IsPremium,
                IsLocked = u.IsLocked,
                DateOfBirth = u.DateOfBirth
            })
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ListeningHistoryDto>> GetUserListeningHistoryAsync(int userId, CancellationToken ct = default)
    {
        var history = await _unitOfWork.Repository<ListeningHistory>().Query()
            .Include(h => h.Song)
                .ThenInclude(s => s!.SongArtists)
                .ThenInclude(sa => sa.Artist)
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ListenedAt)
            .Take(50)
            .ToListAsync(ct);
        
        return history.Select(h => new ListeningHistoryDto
        {
            HistoryId = h.HistoryId,
            UserId = h.UserId,
            SongId = h.SongId,
            SongTitle = h.Song?.Title ?? "Unknown",
            YoutubeVideoId = h.Song?.YoutubeVideoId ?? string.Empty,
            AuthorName = h.Song?.SongArtists.FirstOrDefault()?.Artist?.Name ?? "Unknown Artist",
            ThumbnailUrl = h.Song?.ThumbnailUrl,
            ListenedAt = h.ListenedAt
        });
    }

    public async Task<bool> UpdateUserAsync(UserDto userDto, CancellationToken ct = default)
    {
        return await UpdateUserAsync(new UpdateUserRequest
        {
            UserId = userDto.UserId,
            Username = userDto.Username,
            AvatarUrl = userDto.AvatarUrl,
            DateOfBirth = userDto.DateOfBirth
        }, ct);
    }

    public async Task<bool> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId, ct);
            if (user == null || user.IsDeleted) return false;

            // Check if username is taken by another user
            if (!string.Equals(user.Username, request.Username, StringComparison.OrdinalIgnoreCase))
            {
                bool nameTaken = await _unitOfWork.Repository<User>().AnyAsync(u => u.Username == request.Username && u.UserId != user.UserId, ct);
                if (nameTaken) throw new Exception("Tên người dùng này đã được sử dụng bởi một tài khoản khác.");
            }

            user.Username = request.Username;
            user.AvatarUrl = request.AvatarUrl;
            user.DateOfBirth = request.DateOfBirth;

            _unitOfWork.Repository<User>().Update(user);
            await _unitOfWork.CompleteAsync(ct);
            
            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private UserDto MapToDto(User u) => new UserDto
    {
        UserId = u.UserId,
        Username = u.Username,
        Email = u.Email,
        Role = u.Role,
        AvatarUrl = u.AvatarUrl,
        CreatedAt = u.CreatedAt,
        IsPremium = u.IsPremium,
        IsLocked = u.IsLocked,
        DateOfBirth = u.DateOfBirth
    };
}
