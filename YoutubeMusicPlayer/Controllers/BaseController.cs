using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Controllers;

public abstract class BaseController : Controller
{
    protected int? CurrentUserId
    {
        get
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                // Prioritize "InternalUserId" set by AuthController
                var internalIdClaim = User.FindFirst("InternalUserId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
                if (internalIdClaim != null && int.TryParse(internalIdClaim.Value, out int id))
                {
                    return id;
                }
            }
            return null;
        }
    }

    protected bool IsAdmin => User.IsInRole("Admin");

    protected IActionResult SuccessResponse<T>(T data, string? message = null)
    {
        var response = ApiResponse<T>.SuccessResult(data, message);
        return Json(response);
    }

    protected IActionResult ErrorResponse(string message, string? errorCode = null)
    {
        var response = ApiResponse<object>.ErrorResult(message, errorCode);
        return Json(response);
    }

    protected IActionResult BadRequestResponse(string message, string? errorCode = "BadRequest")
    {
        var response = ApiResponse<object>.ErrorResult(message, errorCode);
        return BadRequest(response);
    }
}
