using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Controllers;

public abstract class BaseController : Controller
{
    protected int? CurrentUserId => User.GetUserId();

    protected bool IsAdmin => User.IsInRole(UserRoles.Admin);

    protected IActionResult SuccessResponse<T>(T data, string? message = null)
    {
        return Ok(ApiResponse<T>.SuccessResult(data, message));
    }

    protected IActionResult ErrorResponse(string message, string? errorCode = "Error")
    {
        return StatusCode(500, ApiResponse<object>.ErrorResult(message, errorCode));
    }

    protected IActionResult BadRequestResponse(string message, string? errorCode = "BadRequest")
    {
        return BadRequest(ApiResponse<object>.ErrorResult(message, errorCode));
    }

    protected IActionResult NotFoundResponse(string message = "Không tìm thấy dữ liệu")
    {
        return NotFound(ApiResponse<object>.ErrorResult(message, "NotFound"));
    }

    protected IActionResult ForbiddenResponse(string message = "Bạn không có quyền thực hiện hành động này")
    {
        return StatusCode(403, ApiResponse<object>.ErrorResult(message, "Forbidden"));
    }
}
