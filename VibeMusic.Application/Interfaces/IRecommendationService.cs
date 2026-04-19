using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IRecommendationService
{
    /// <summary>
    /// The core Hybrid-AI recommendation engine. 
    /// Generates context-aware, personalized music discoveries.
    /// </summary>
    Task<IEnumerable<YoutubeVideoDetails>> GetSmartDiscoveryAsync(string currentVideoId, int? userId = null);

    /// <summary>
    /// Generates a "Daily Mix" for the user. Supports prefetched weights to avoid DbContext concurrency.
    /// </summary>
    Task<IEnumerable<YoutubeVideoDetails>> GetDailyMixAsync(int userId, Dictionary<string, double>? prefetchedWeights = null, bool forceRefresh = false);

    /// <summary>
    /// Generates a specific variant of the Daily Mix (e.g. Mix 1, Mix 2).
    /// </summary>
    Task<IEnumerable<YoutubeVideoDetails>> GetDailyMixVariantAsync(int userId, int variantIndex, Dictionary<string, double>? prefetchedWeights = null, bool forceRefresh = false);

    /// <summary>
    /// Generates a "Because you listened to [Artist]" section. Supports prefetched artist name to avoid DbContext concurrency.
    /// </summary>
    Task<IEnumerable<YoutubeVideoDetails>> GetBecauseYouListenedToAsync(int userId, string? prefetchedArtistName = null, bool forceRefresh = false);

    /// <summary>
    /// Generates mood-based music recommendations (e.g., Chill, Focus, Workout).
    /// </summary>
    Task<IEnumerable<YoutubeVideoDetails>> GetMoodMusicAsync(string moodTag, int limit = 12, bool forceRefresh = false);

    /// <summary>
    /// Fetches long music compilations and mixes.
    /// </summary>
    Task<IEnumerable<YoutubeVideoDetails>> GetCompilationsAsync(int limit = 12, bool forceRefresh = false);
}
