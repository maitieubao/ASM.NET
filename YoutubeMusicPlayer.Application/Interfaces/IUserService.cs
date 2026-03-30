using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserDto?> GetUserByIdAsync(int id);
    Task<bool> ToggleUserLockAsync(int id);
    Task<bool> DeleteUserAsync(int id);
    Task<IEnumerable<UserDto>> SearchUsersAsync(string query);
    Task<IEnumerable<ListeningHistoryDto>> GetUserListeningHistoryAsync(int userId);
    Task<bool> UpdateUserAsync(UserDto userDto);
}
