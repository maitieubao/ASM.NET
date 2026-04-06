using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Infrastructure.External.AiPlugins;
using System.Text.Json;

namespace YoutubeMusicPlayer.Infrastructure.External;

#pragma warning disable SKEXP0070 // Experimental feature

public class SemanticKernelAgentService : IAiAgentService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletionService;

    public SemanticKernelAgentService(
        IConfiguration configuration,
        IYoutubeService youtubeService,
        IDeezerService deezerService,
        IInteractionService interactionService,
        ISongService songService,
        ISubscriptionService subscriptionService,
        IPlaylistService playlistService,
        IWikipediaService wikipediaService)
    {
        var apiKey = configuration["Groq:ApiKey"];
        var modelId = configuration["Groq:ModelId"] ?? "llama-3.3-70b-versatile";
        var baseUrl = configuration["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1";

        // Performance & Reliability: Add explicit HttpClient with 30s timeout and custom BaseUrl for Groq
        var httpClient = new System.Net.Http.HttpClient { 
            Timeout = TimeSpan.FromSeconds(30),
            BaseAddress = new Uri(baseUrl)
        };
        
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(modelId, apiKey!, httpClient: httpClient);
        
        // Performance & Reliability: Custom headers if needed for DeepSeek can be added via HttpClient

        // Register Plugins to match the AI Assistant's capabilities (simplified names to prevent LLM hallucinations)
        builder.Plugins.AddFromObject(new MusicSearchPlugin(youtubeService, deezerService), "Music");
        builder.Plugins.AddFromObject(new UserInteractionPlugin(interactionService, songService, subscriptionService), "User");
        builder.Plugins.AddFromObject(new PlaylistPlugin(playlistService, songService), "Playlist");
        builder.Plugins.AddFromObject(new WikipediaPlugin(wikipediaService), "Info");

        _kernel = builder.Build();
        _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<AgentResponse> ProcessCommandAsync(int? userId, string userMessage, List<ChatMessageDto>? history = null)
    {
        try {
            var chatHistory = new ChatHistory();
            
            // System Prompt
            string systemPrompt = "Bạn là trợ lý âm nhạc thông minh 'Antigravity Music'.\n" +
                                   "Nhiệm vụ: Tìm nhạc, quản lý playlist, xem lịch sử và thông tin nghệ sĩ.\n" +
                                   (userId.HasValue ? $"ID người dùng hiện tại: {userId.Value}.\n" : "Người dùng chưa đăng nhập.\n") +
                                   "Phong cách: Thân thiện, chuyên nghiệp, súc tích bằng tiếng Việt.\n" +
                                   " QUY TẮC QUAN TRỌNG:\n" +
                                   "1. Khi người dùng muốn nghe nhạc, hãy tìm kiếm và gợi ý bài hát.\n" +
                                   "2. Nếu quyết định phát một bài cụ thể, hãy thêm dòng 'ACTION:play:[VideoID]' vào CUỐI câu trả lời.\n" +
                                   "3. Giới hạn 5 bài hát mỗi lần gợi ý.\n" +
                                   "4. Luôn ưu tiên dùng các công cụ (plugins) có sẵn để tra cứu thông tin chính xác.";

            chatHistory.AddSystemMessage(systemPrompt);

            // Load historical context
            if (history != null && history.Any())
            {
                foreach (var msg in history.TakeLast(8)) 
                {
                    if (msg.Role == "assistant") chatHistory.AddAssistantMessage(msg.Content);
                    else chatHistory.AddUserMessage(msg.Content);
                }
            }

            chatHistory.AddUserMessage(userMessage);

            var settings = new OpenAIPromptExecutionSettings 
            { 
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };
            
            var result = await _chatCompletionService.GetChatMessageContentAsync(
                chatHistory, 
                executionSettings: settings, 
                kernel: _kernel);

            var responseText = result.Content ?? "Tôi không tìm thấy thông tin phù hợp.";
            var response = new AgentResponse { Message = responseText };

            // Suggested Action Extraction (Robust version)
            var match = System.Text.RegularExpressions.Regex.Match(responseText, @"ACTION:play:([a-zA-Z0-9_-]{11})");
            if (match.Success)
            {
                response.SuggestedAction = "play:" + match.Groups[1].Value;
            }
            else if (responseText.Contains("ACTION:play:"))
            {
                // Fallback for non-standard IDs if any
                var parts = responseText.Split("ACTION:play:");
                var potentialId = parts.Last().Split(new[] { ' ', '\n', '<', ']', ')' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(potentialId))
                {
                    response.SuggestedAction = "play:" + potentialId.Replace("[", "").Replace("]", "");
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AiAgent] Error: {ex.Message}");
            return new AgentResponse { 
                Message = "Rất tiếc, tôi đang gặp chút vấn đề khi kết nối với máy chủ AI. Bạn hãy thử lại sau vài giây nhé!" 
            };
        }
    }
}
