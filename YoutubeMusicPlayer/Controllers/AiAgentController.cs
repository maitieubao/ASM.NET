using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Route("api/[controller]")]
public class AiAgentController : BaseController
{
    private readonly IAiAgentService _aiAgentService;

    public AiAgentController(IAiAgentService aiAgentService)
    {
        _aiAgentService = aiAgentService;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrEmpty(request.Message))
            return BadRequestResponse("Message cannot be empty.");

        var response = await _aiAgentService.ProcessCommandAsync(CurrentUserId, request.Message, request.History);
        return SuccessResponse(response);
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessageDto>? History { get; set; }
}
