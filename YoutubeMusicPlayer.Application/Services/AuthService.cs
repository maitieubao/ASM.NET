using System;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;
using BCrypt.Net;

namespace YoutubeMusicPlayer.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuthService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UserDto?> AuthenticateAsync(string email, string password)
    {
        var user = await _unitOfWork.Repository<User>().FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || user.PasswordHash == null) return null;

        if (user.IsLocked)
            throw new Exception("Your account has been locked. Please contact support.");

        bool isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (!isValid) return null;

        return MapToDto(user);
    }

    public async Task<UserDto> RegisterAsync(RegisterDto dto)
    {
        var existingUser = await _unitOfWork.Repository<User>().FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (existingUser != null)
            throw new Exception("Email is already registered.");

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Customer",
            CreatedAt = DateTime.UtcNow,
            DateOfBirth = dto.DateOfBirth
        };

        await _unitOfWork.Repository<User>().AddAsync(user);
        await _unitOfWork.CompleteAsync();

        return MapToDto(user);
    }

    public async Task<UserDto> AuthenticateGoogleUserAsync(string email, string name, string googleId, string? avatarUrl)
    {
        var user = await _unitOfWork.Repository<User>().FirstOrDefaultAsync(u => u.Email == email);
        
        if (user != null && user.IsLocked)
            throw new Exception("Your account has been locked. Please contact support.");

        if (user == null)
        {
            user = new User
            {
                Email = email,
                Username = name,
                GoogleId = googleId,
                AvatarUrl = avatarUrl,
                Role = "Customer",
                CreatedAt = DateTime.UtcNow,
                PasswordHash = "GOOGLE_AUTH_USER"
            };
            await _unitOfWork.Repository<User>().AddAsync(user);
            await _unitOfWork.CompleteAsync();
        }
        else if (user.GoogleId == null)
        {
            user.GoogleId = googleId;
            if (string.IsNullOrEmpty(user.AvatarUrl))
                user.AvatarUrl = avatarUrl;
            _unitOfWork.Repository<User>().Update(user);
            await _unitOfWork.CompleteAsync();
        }

        return MapToDto(user);
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId);
        if (user == null) return null;
        return MapToDto(user);
    }

    private UserDto MapToDto(User user)
    {
        return new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            AvatarUrl = user.AvatarUrl,
            CreatedAt = user.CreatedAt,
            IsPremium = user.IsPremium,
            IsLocked = user.IsLocked,
            DateOfBirth = user.DateOfBirth
        };
    }
}
