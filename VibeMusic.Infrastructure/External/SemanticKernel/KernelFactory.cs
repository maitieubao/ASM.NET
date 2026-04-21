using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using VibeMusic.Application.Interfaces;
using VibeMusic.Infrastructure.External.AiPlugins;

namespace VibeMusic.Infrastructure.External.SemanticKernel;

/// <summary>
/// Builds and configures the Semantic Kernel instance with all plugins registered.
/// </summary>
public static class KernelFactory
{
    public static Kernel Build(
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
        IWikipediaService wikipediaService)
    {
        var apiKey  = configuration["Groq:ApiKey"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Groq:ApiKey cho AI engine.");
        var modelId = configuration["Groq:ModelId"] ?? "llama-3.3-70b-versatile";
        var baseUrl = configuration["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1";

        var httpClient = new System.Net.Http.HttpClient
        {
            Timeout     = TimeSpan.FromSeconds(90),
            BaseAddress = new Uri(baseUrl)
        };

        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(modelId, apiKey, httpClient: httpClient);

        // ── Plugins ──────────────────────────────────────────────────────────
        builder.Plugins.AddFromObject(
            new MusicSearchPlugin(youtubeService, deezerService), "Music");

        builder.Plugins.AddFromObject(
            new UserInteractionPlugin(interactionService, songService, subscriptionService, userAccessGuard), "User");

        builder.Plugins.AddFromObject(
            new PlaylistPlugin(playlistService, songService, interactionService, userAccessGuard), "Playlist");

        builder.Plugins.AddFromObject(
            new UserExperiencePlugin(
                userService, recommendationService, notificationService, commentService,
                subscriptionService, playlistService, songService,
                profileFacade, playbackFacade, homeFacade, userAccessGuard),
            "UserApp");

        builder.Plugins.AddFromObject(new WikipediaPlugin(wikipediaService), "Info");

        return builder.Build();
    }
}
