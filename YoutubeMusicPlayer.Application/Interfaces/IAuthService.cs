using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Domain.Entities;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IAuthService
{
    Task<UserDto?> AuthenticateAsync(string email, string password);
    Task<UserDto> RegisterAsync(RegisterDto registerDto);
    Task<UserDto> AuthenticateGoogleUserAsync(string email, string name, string googleId, string? avatarUrl);
    Task<UserDto?> GetUserByIdAsync(int userId);
}
