using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.Common;
using Microsoft.AspNetCore.Http;
using System;

namespace YoutubeMusicPlayer.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AiAgentController : BaseController
{
    private readonly IAiAgentService _aiAgentService;
    private readonly ILogger<AiAgentController> _logger;

    public AiAgentController(IAiAgentService aiAgentService, ILogger<AiAgentController> logger)
    {
        _aiAgentService = aiAgentService;
        _logger = logger;
    }

    /// <summary>
    /// Gửi tin nhắn hoặc lệnh điều khiển cho Trợ lý AI.
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ApiResponse<AgentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequestResponse("Tin nhắn không được để trống hoặc chỉ toàn khoảng trắng.");

        try
        {
            _logger.LogInformation("Người dùng {UserId} đang gửi lệnh AI: {Message}", CurrentUserId, request.Message);

            var response = await _aiAgentService.ProcessCommandAsync(
                CurrentUserId, 
                request.Message, 
                request.History ?? new List<ChatMessageDto>());

            return SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xử lý lệnh AI cho người dùng {UserId}", CurrentUserId);
            return ErrorResponse("Đã xảy ra lỗi khi xử lý yêu cầu của bạn. Vui lòng thử lại sau.");
        }
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessageDto> History { get; set; } = new();
}
