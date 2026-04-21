namespace VibeMusic.Application.Interfaces;

public interface IYoutubeSearchService
{
    Task<IEnumerable<YoutubeVideoDetails>> SearchVideosAsync(
        string query, int limit = 30, bool searchCompilations = false);
    Task<IEnumerable<YoutubeVideoDetails>> GetTrendingMusicAsync(
        int limit = 15, bool forceRefresh = false);
}
