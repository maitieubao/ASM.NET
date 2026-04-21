using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using VibeMusic.Application.Interfaces;
using VibeMusic.Infrastructure.External.AiPlugins;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace VibeMusic.Infrastructure.External;

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
        IUserService userService,
        IRecommendationService recommendationService,
        INotificationService notificationService,
        ICommentService commentService,
        IHomeFacade homeFacade,
        IPlaybackFacade playbackFacade,
        IProfileFacade profileFacade,
        AiUserAccessGuard userAccessGuard,
        IWikipediaService wikipediaService,
        ILogger<SemanticKernelAgentService> logger)
    {
        _logger = logger;
        var apiKey = configuration["Groq:ApiKey"];
        var modelId = configuration["Groq:ModelId"] ?? "llama-3.3-70b-versatile";
        var baseUrl = configuration["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1";
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Thiếu cấu hình Groq:ApiKey cho AI engine.");

        // Performance & Reliability: Add explicit HttpClient with 90s timeout and custom BaseUrl for Groq
        var httpClient = new System.Net.Http.HttpClient { 
            Timeout = TimeSpan.FromSeconds(90),
            BaseAddress = new Uri(baseUrl)
        };
        
        // ORCHESTRATION: We use OpenAI SDK connectors pointed at Groq for high-speed inference.
        // Groq provides the Llama 3/70B model which is excellent for Vietnamese music intents.
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(modelId, apiKey!, httpClient: httpClient);
        
        // PLUGIN STRATEGY: Tool-Calling Capability
        // We import several native C# classes as AI-callable tools. 
        // This allows the LLM to trigger real-world actions (DB writes, external searches).
        builder.Plugins.AddFromObject(new MusicSearchPlugin(youtubeService, deezerService), "Music");
        builder.Plugins.AddFromObject(new UserInteractionPlugin(interactionService, songService, subscriptionService, userAccessGuard), "User");
        builder.Plugins.AddFromObject(new PlaylistPlugin(playlistService, songService, interactionService, userAccessGuard), "Playlist");
        builder.Plugins.AddFromObject(
            new UserExperiencePlugin(
                userService,
                recommendationService,
                notificationService,
                commentService,
                subscriptionService,
                playlistService,
                songService,
                profileFacade,
                playbackFacade,
                homeFacade,
                userAccessGuard),
            "UserApp");
        builder.Plugins.AddFromObject(new WikipediaPlugin(wikipediaService), "Info");

        _kernel = builder.Build();
        _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
    }

    /// <summary>
    /// Processes a natural language command using Semantic Kernel's Auto-Invocation mode.
    /// This is the entry point for the "Antigravity AI" assistant.
    /// </summary>
    public async Task<AgentResponse> ProcessCommandAsync(int? userId, string userMessage, List<ChatMessageDto>? history = null)
    {
        try {
            var chatHistory = new ChatHistory();
            
            // SYSTEM PROMPT: Defines the AI's persona and strict rules for Tool Usage and Action Extraction.
            // Rule #1: Always respond in natural Vietnamese.
            // Rule #2: Use the ACTION:[keyword]:[id] format for player control navigation.
            string systemPrompt = "Bạn là trợ lý âm nhạc 'Antigravity AI'. Thân thiện, chuyên nghiệp bằng tiếng Việt.\n" +
                                   "Nhiệm vụ: Tìm nhạc, quản lý playlist, tra cứu nghệ sĩ, hỗ trợ tính năng cá nhân hóa người dùng.\n" +
                                   (userId.HasValue ? $"User ID: {userId.Value}.\n" : "") +
                                   "QUY TẮC QUAN TRỌNG:\n" +
                                   "0. BẢO MẬT USER ID: Chỉ được thao tác dữ liệu của chính người dùng hiện tại. Không được suy đoán userId khác.\n" +
                                   "1. LUÔN TRÒ CHUYỆN: Phải luôn có câu trả lời bằng văn bản tự nhiên gửi tới người dùng. KHÔNG ĐƯỢC chỉ gửi mỗi lệnh ACTION.\n" +
                                   "2. Tìm bài hát: Dùng công cụ SEARCH khi người dùng yêu cầu bài cụ thể hoặc tìm danh sách theo chủ đề.\n" +
                                   "3. Phát nhạc: Nếu muốn phát nhạc, hãy thêm 'ACTION:play:[VideoID]' vào CUỐI câu phản hồi.\n" +
                                   "4. Playlist thông minh: Khi user yêu cầu tạo playlist từ lịch sử/like gần đây, PHẢI gọi plugin Playlist phù hợp thay vì trả lời chung chung.\n" +
                                   "5. Ví dụ bắt buộc dùng plugin:\n" +
                                   "- 'Tạo playlist tên X gồm 10 bài hát gần nhất tôi like' => gọi CreatePlaylistFromRecentLikedSongs(userId, 'X', 10).\n" +
                                   "- 'Tạo playlist tên X gồm 10 bài hát gần nhất tôi nghe của Ed Sheeran' => gọi CreatePlaylistFromRecentListeningHistory(userId, 'X', 10, 'Ed Sheeran').\n" +
                                   "6. Chỉnh sửa playlist: dùng UpdatePlaylistInfo để đổi tên/mô tả/quyền riêng tư; dùng AddSongToPlaylist hoặc RemoveSongFromPlaylist để cập nhật bài hát.\n" +
                                   "7. Sau khi tạo playlist thành công, thêm 'ACTION:navigate:playlist:[PlaylistId]' ở cuối để mở playlist.\n" +
                                   "8. Ưu tiên dùng plugin UserApp cho các nhu cầu tài khoản: hồ sơ, thông báo, premium, comment, daily mix.\n" +
                                   "9. Súc tích: Trả lời ngắn gọn nhưng đầy đủ ý, không lặp lại thông tin dư thừa.";

            chatHistory.AddSystemMessage(systemPrompt);

            // Load context (last 6 messages to keep window small and efficient)
            if (history != null && history.Any())
            {
                foreach (var msg in history.TakeLast(6)) 
                {
                    if (msg.Role == "assistant") chatHistory.AddAssistantMessage(msg.Content);
                    else chatHistory.AddUserMessage(msg.Content);
                }
            }

            chatHistory.AddUserMessage(userMessage);

            // EXECUTION SETTINGS: AutoInvokeKernelFunctions is critical.
            // It allows the LLM to call our native C# methods automatically if it deems them necessary.
            var settings = new OpenAIPromptExecutionSettings 
            { 
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 700,
                Temperature = 0.6
            };

            // RELIABILITY - RETRY LOGIC:
            // High-traffic providers like Groq frequently return 429 (Rate Limit).
            // We implement a "Smart Retry" that parses the 'Retry-After' header from the error message.
            Microsoft.SemanticKernel.ChatMessageContent? result = null;
            int retries = 3;
            for (int attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    result = await _chatCompletionService.GetChatMessageContentAsync(
                        chatHistory, 
                        executionSettings: settings, 
                        kernel: _kernel);
                    break; // Success
                }
                catch (Exception ex) when (attempt < retries)
                {
                    var delay = GetRetryDelay(ex, attempt);
                    _logger.LogWarning(
                        ex,
                        "[AiAgent] Transient error in SK. Retrying... ({Retries} left), waiting {DelayMs}ms",
                        retries - attempt,
                        (int)delay.TotalMilliseconds);

                    await Task.Delay(delay);
                }
                catch (Exception) { throw; } // Final failure
            }

            if (result == null) throw new Exception("AI returned empty result");

            var responseText = result.Content ?? "Tôi không tìm thấy thông tin phù hợp.";
            var response = new AgentResponse { Message = responseText };

            // Suggested Action Extraction
            var match = Regex.Match(responseText, @"ACTION:play:([a-zA-Z0-9_-]{11})");
            if (match.Success)
            {
                response.SuggestedAction = "play:" + match.Groups[1].Value;
            }
            else
            {
                var playlistNavMatch = Regex.Match(responseText, @"ACTION:navigate:playlist:(\d+)");
                if (playlistNavMatch.Success)
                {
                    response.SuggestedAction = "navigate:playlist:" + playlistNavMatch.Groups[1].Value;
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiAgent] ProcessCommandAsync failed");
            return new AgentResponse { 
                Message = BuildUserFriendlyErrorMessage(ex)
            };
        }
    }

    private static TimeSpan GetRetryDelay(Exception ex, int attempt)
    {
        var text = ex.ToString();
        var retryAfterMatch = Regex.Match(text, @"Please try again in\s+([0-9]+(?:\.[0-9]+)?)s", RegexOptions.IgnoreCase);

        if (retryAfterMatch.Success &&
            double.TryParse(retryAfterMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var seconds) &&
            seconds > 0)
        {
            // Add small buffer for clock/network jitter.
            return TimeSpan.FromSeconds(seconds + 0.5);
        }

        // Exponential backoff fallback for other transient failures.
        var fallbackSeconds = Math.Min(Math.Pow(2, attempt), 10);
        return TimeSpan.FromSeconds(fallbackSeconds);
    }

    private static string BuildUserFriendlyErrorMessage(Exception ex)
    {
        var text = ex.ToString();
        if (text.Contains("rate_limit_exceeded", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("HTTP 429", StringComparison.OrdinalIgnoreCase))
        {
            return "AI đang bận do quá nhiều yêu cầu cùng lúc. Bạn vui lòng thử lại sau 15-20 giây nhé.";
        }

        return "Rất tiếc, máy chủ AI đang bận hoặc gặp sự cố kết nối. Bạn vui lòng thử lại sau giây lát nhé!";
    }
}
