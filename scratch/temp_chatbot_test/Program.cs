using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChatbotTest;

class Program
{
    static async Task Main(string[] args)
    {
        // Setup Configuration Mock
        var config = new ConfigurationBuilder().Build();
        
        // Groq Details from appsettings.json
        string apiKey = "YOUR_GROQ_API_KEY_HERE"; // Removed for security
        string modelId = "llama-3.3-70b-versatile";
        string baseUrl = "https://api.groq.com/openai/v1";

        var httpClient = new System.Net.Http.HttpClient { 
            Timeout = TimeSpan.FromSeconds(30),
            BaseAddress = new Uri(baseUrl)
        };

        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(modelId, apiKey, httpClient: httpClient);
        
        var kernel = builder.Build();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        string systemPrompt = "Bạn là trợ lý âm nhạc thông minh 'Antigravity Music'.\n" +
                               "Nhiệm vụ: Tìm nhạc, quản lý playlist, xem lịch sử và thông tin nghệ sĩ.\n" +
                               "Phong cách: Thân thiện, chuyên nghiệp, súc tích bằng tiếng Việt.\n" +
                               "QUY TẮC QUAN TRỌNG:\n" +
                               "1. Khi người dùng muốn nghe nhạc, hãy tìm kiếm và gợi ý bài hát.\n" +
                               "2. Nếu quyết định phát một bài cụ thể, hãy thêm dòng 'ACTION:play:[VideoID]' vào CUỐI câu trả lời.\n" +
                               "3. Giới hạn 5 bài hát mỗi lần gợi ý.";
        history.AddSystemMessage(systemPrompt);

        var testQueries = new[] {
            "Chào bạn, bạn có thể giúp gì cho tôi?",
            "Tôi muốn nghe bài hát 'Lạc Trôi' của Sơn Tùng M-TP.",
            "Bạn nghĩ sao về âm nhạc của Việt Nam hiện nay?"
        };

        using var writer = new StreamWriter(@"C:\Users\maiti\OneDrive\Desktop\ASM.NET\chatbot_test_results.txt");
        await writer.WriteLineAsync($"AI Chatbot Test Results - {DateTime.Now}");
        await writer.WriteLineAsync("========================================");

        foreach (var query in testQueries)
        {
            Console.WriteLine($"User: {query}");
            await writer.WriteLineAsync($"[USER]: {query}");
            
            history.AddUserMessage(query);
            
            var result = await chatService.GetChatMessageContentAsync(history, kernel: kernel);
            
            var responseText = result.Content ?? "";
            Console.WriteLine($"AI: {responseText}");
            await writer.WriteLineAsync($"[AI]: {responseText}");
            
            // Interaction Check
            if (responseText.Contains("ACTION:play:")) {
                await writer.WriteLineAsync($"[DETECTION]: Action Trigger Detected!");
            }
            
            history.AddAssistantMessage(responseText);
            await writer.WriteLineAsync("----------------------------------------");
        }

        Console.WriteLine("\nTest completed. See chatbot_test_results.txt");
    }
}

public class MockMusicPlugin
{
    [KernelFunction, System.ComponentModel.Description("Tìm kiếm bài hát")]
    public string Search(string query)
    {
        if (query.ToLower().Contains("sơn tùng")) 
            return "[Result: Lạc Trôi - VideoID: oRdxUFDoQe0, Em Của Ngày Hôm Qua - VideoID: 5i9yYmXGeyA]";
        
        return "[Result: Bài hát thư giãn 1 - VideoID: id1, Bài hát thư giãn 2 - VideoID: id2, Bài hát thư giãn 3 - VideoID: id3]";
    }
}
