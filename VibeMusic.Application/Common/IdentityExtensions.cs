using System.Security.Claims;

namespace YoutubeMusicPlayer.Application.Common;

public static class IdentityExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return null;

        var claim = user.FindFirst(AuthConstants.InternalUserIdClaim) ?? 
                    user.FindFirst(ClaimTypes.NameIdentifier);

        if (claim != null && int.TryParse(claim.Value, out int id))
        {
            return id;
        }

        return null;
    }
}
