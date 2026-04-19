using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using YoutubeMusicPlayer.Application.Common;

namespace YoutubeMusicPlayer.Infrastructure.External.AiPlugins;

public class AiUserAccessGuard
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AiUserAccessGuard(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int GetCurrentUserIdOrThrow()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var currentUserId = user?.GetUserId();
        if (!currentUserId.HasValue)
            throw new UnauthorizedAccessException("Không xác định được người dùng hiện tại.");

        return currentUserId.Value;
    }

    public int EnsureAuthorizedUser(int requestedUserId)
    {
        var currentUserId = GetCurrentUserIdOrThrow();
        if (requestedUserId != currentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập dữ liệu của người dùng khác.");

        return currentUserId;
    }

    public int EnsureAuthorizedOrUseCurrent(int? requestedUserId = null)
    {
        var currentUserId = GetCurrentUserIdOrThrow();
        if (requestedUserId.HasValue && requestedUserId.Value != currentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập dữ liệu của người dùng khác.");

        return currentUserId;
    }
}
