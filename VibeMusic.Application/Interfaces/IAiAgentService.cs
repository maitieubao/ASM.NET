using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Application.Interfaces;

public class ChatMessageDto
{
    public string Role { get; set; } = "user"; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
}

public class AgentResponse
{
    public string Message { get; set; } = string.Empty;
    public string? SuggestedAction { get; set; } // e.g., "play:videoID", "navigate:playlistID"
    public object? Data { get; set; }
}

public interface IAiAgentService
{
    Task<AgentResponse> ProcessCommandAsync(int? userId, string userMessage, List<ChatMessageDto>? history = null);
}
