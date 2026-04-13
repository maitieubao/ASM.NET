using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Infrastructure.External.AiPlugins;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace YoutubeMusicPlayer.Infrastructure.External;

#pragma warning disable SKEXP0070 // Experimental feature

public class SemanticKernelAgentService : IAiAgentService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly ILogger<SemanticKernelAgentService> _logger;

    public SemanticKernelAgentService(
        IConfiguration configuration,
        IYoutubeService youtubeService,
        IDeezerService deezerService,
        IInteractionService interactionService,
        ISongService songService,
        ISubscriptionService subscriptionService,
        IPlaylistService playlistService,
        IWikipediaService wikipediaService,
        ILogger<SemanticKernelAgentService> logger)
    {
        _logger = logger;
        var apiKey = configuration["Groq:ApiKey"];
        var modelId = configuration["Groq:ModelId"] ?? "llama-3.3-70b-versatile";
        var baseUrl = configuration["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1";

        // Performance & Reliability: Add explicit HttpClient with 90s timeout and custom BaseUrl for Groq
        var httpClient = new System.Net.Http.HttpClient { 
            Timeout = TimeSpan.FromSeconds(90),
            BaseAddress = new Uri(baseUrl)
        };
        
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(modelId, apiKey!, httpClient: httpClient);
        
        // Performance & Reliability: Custom headers if needed for DeepSeek can be added via HttpClient

        // Register Plugins (Simple names to avoid Groq tool-calling errors)
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
            
            // System Prompt (Optimized for Groq & Natural Conversation)
            string systemPrompt = "Bạn là trợ lý âm nhạc 'Antigravity AI'. Thân thiện, chuyên nghiệp bằng tiếng Việt.\n" +
                                   "Nhiệm vụ: Tìm nhạc, quản lý playlist, tra cứu nghệ sĩ.\n" +
                                   (userId.HasValue ? $"User ID: {userId.Value}.\n" : "") +
                                   "QUY TẮC QUAN TRỌNG:\n" +
                                   "1. LUÔN TRÒ CHUYỆN: Phải luôn có câu trả lời bằng văn bản tự nhiên gửi tới người dùng. KHÔNG ĐƯỢC chỉ gửi mỗi lệnh ACTION.\n" +
                                   "2. Tìm bài hát: Dùng công cụ SEARCH khi người dùng yêu cầu bài cụ thể hoặc tìm danh sách theo chủ đề.\n" +
                                   "3. Phát nhạc: Nếu muốn phát nhạc, hãy thêm 'ACTION:play:[VideoID]' vào CUỐI câu phản hồi.\n" +
                                   "4. Súc tích: Trả lời ngắn gọn nhưng đầy đủ ý, không lặp lại thông tin dư thừa.";

            chatHistory.AddSystemMessage(systemPrompt);

            // Load context
            if (history != null && history.Any())
            {
                foreach (var msg in history.TakeLast(6)) 
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

            // Reliability: Retry mechanism for transient LLM/Network errors
            Microsoft.SemanticKernel.ChatMessageContent? result = null;
            int retries = 3;
            while (retries > 0)
            {
                try
                {
                    result = await _chatCompletionService.GetChatMessageContentAsync(
                        chatHistory, 
                        executionSettings: settings, 
                        kernel: _kernel);
                    break; // Success
                }
                catch (Exception ex) when (retries > 1)
                {
                    _logger.LogWarning(ex, "[AiAgent] Transient error in SK. Retrying... ({Retries} left)", retries - 1);
                    retries--;
                    await Task.Delay(1000); // Backoff
                }
                catch (Exception) { throw; } // Final failure
            }

            if (result == null) throw new Exception("AI returned empty result");

            var responseText = result.Content ?? "Tôi không tìm thấy thông tin phù hợp.";
            var response = new AgentResponse { Message = responseText };

            // Suggested Action Extraction
            var match = System.Text.RegularExpressions.Regex.Match(responseText, @"ACTION:play:([a-zA-Z0-9_-]{11})");
            if (match.Success)
            {
                response.SuggestedAction = "play:" + match.Groups[1].Value;
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiAgent] ProcessCommandAsync failed");
            return new AgentResponse { 
                Message = "Rất tiếc, máy chủ AI đang bận hoặc gặp sự cố kết nối. Bạn vui lòng thử lại sau giây lát nhé!" 
            };
        }
    }
}
