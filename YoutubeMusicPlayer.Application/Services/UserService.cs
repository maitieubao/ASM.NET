using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _unitOfWork.Repository<User>().GetAllAsync();
        return users.Select(u => new UserDto
        {
            UserId = u.UserId,
            Username = u.Username,
            Email = u.Email,
            Role = u.Role,
            AvatarUrl = u.AvatarUrl,
            CreatedAt = u.CreatedAt,
            IsPremium = u.IsPremium,
            IsLocked = u.IsLocked
        });
    }

    public async Task<UserDto?> GetUserByIdAsync(int id)
    {
        var u = await _unitOfWork.Repository<User>().GetByIdAsync(id);
        if (u == null) return null;

        return new UserDto
        {
            UserId = u.UserId,
            Username = u.Username,
            Email = u.Email,
            Role = u.Role,
            AvatarUrl = u.AvatarUrl,
            CreatedAt = u.CreatedAt,
            IsPremium = u.IsPremium,
            IsLocked = u.IsLocked
        };
    }

    public async Task<bool> ToggleUserLockAsync(int id)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(id);
        if (user == null) return false;

        user.IsLocked = !user.IsLocked;
        _unitOfWork.Repository<User>().Update(user);
        await _unitOfWork.CompleteAsync();
        return true;
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(id);
        if (user == null) return false;

        _unitOfWork.Repository<User>().Remove(user);
        await _unitOfWork.CompleteAsync();
        return true;
    }

    public async Task<IEnumerable<UserDto>> SearchUsersAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return await GetAllUsersAsync();

        var users = await _unitOfWork.Repository<User>().FindAsync(u => 
            u.Username.Contains(query) || u.Email.Contains(query));
        
        return users.Select(u => new UserDto
        {
            UserId = u.UserId,
            Username = u.Username,
            Email = u.Email,
            Role = u.Role,
            AvatarUrl = u.AvatarUrl,
            CreatedAt = u.CreatedAt,
            IsPremium = u.IsPremium,
            IsLocked = u.IsLocked
        });
    }

    public async Task<IEnumerable<ListeningHistoryDto>> GetUserListeningHistoryAsync(int userId)
    {
        var history = await _unitOfWork.Repository<ListeningHistory>().FindAsync(h => h.UserId == userId);
        
        // Load songs to get titles
        var result = new List<ListeningHistoryDto>();
        foreach(var h in history.OrderByDescending(x => x.ListenedAt))
        {
            var song = await _unitOfWork.Repository<Song>().GetByIdAsync(h.SongId);
            result.Add(new ListeningHistoryDto
            {
                HistoryId = h.HistoryId,
                UserId = h.UserId,
                SongId = h.SongId,
                SongTitle = song?.Title ?? "Unknown",
                ThumbnailUrl = song?.ThumbnailUrl,
                ListenedAt = h.ListenedAt
            });
        }
        return result;
    }
    public async Task<bool> UpdateUserAsync(UserDto userDto)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userDto.UserId);
        if (user == null) return false;

        user.Username = userDto.Username;
        user.AvatarUrl = userDto.AvatarUrl;
        user.DateOfBirth = userDto.DateOfBirth;

        _unitOfWork.Repository<User>().Update(user);
        await _unitOfWork.CompleteAsync();
        return true;
    }
}
