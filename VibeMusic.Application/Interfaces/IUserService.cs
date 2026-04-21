using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using VibeMusic.Application.DTOs;

namespace VibeMusic.Application.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken ct = default);
    Task<(IEnumerable<UserDto> Users, int TotalCount)> GetPaginatedUsersAsync(int page, int pageSize, string? searchTerm = null, CancellationToken ct = default);
    Task<UserDto?> GetUserByIdAsync(int id, CancellationToken ct = default);
    Task<bool> ToggleUserLockAsync(int id, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<UserDto>> SearchUsersAsync(string query, CancellationToken ct = default);
    Task<IEnumerable<ListeningHistoryDto>> GetUserListeningHistoryAsync(int userId, CancellationToken ct = default);
    Task<bool> UpdateUserAsync(UserDto userDto, CancellationToken ct = default);
    Task<bool> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default);
    Task<bool> GrantPremiumByPlanAsync(int userId, int planId, CancellationToken ct = default);
    Task<bool> RevokePremiumAsync(int userId, CancellationToken ct = default);
}
