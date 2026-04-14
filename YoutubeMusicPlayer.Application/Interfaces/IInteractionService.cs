using System.Collections.Generic;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IInteractionService
{
    Task RecordListeningHistoryAsync(int userId, int songId);
    Task<IEnumerable<int>> GetRecentListeningHistoryAsync(int userId, int count = 20);
    Task RecordSearchHistoryAsync(int userId, string query);
    Task<IEnumerable<string>> GetRecentSearchHistoryAsync(int userId, int count = 10);
    
    // Advanced Tracking (A+ Recommendation features)
    Task UpdateListeningStatsAsync(int userId, int songId, double durationSeconds);
    Task IncrementPlayCountAsync(int songId);
    Task<IEnumerable<string>> GetTopPreferredGenresAsync(int userId, int count = 5);
    Task<Dictionary<string, double>> GetTopPreferredGenresWithWeightsAsync(int userId, int count = 5);
    Task<IEnumerable<string>> GetTopPreferredArtistsAsync(int userId, int count = 10);
    Task<List<string>> GetHistoryVideoIdsAsync(int userId, int count = 50);
    
    // Song Likes / Favorites
    Task<bool> ToggleLikeAsync(int userId, int songId);
    Task<bool> IsSongLikedAsync(int userId, int songId);
    Task<IEnumerable<int>> GetLikedSongIdsAsync(int userId);
    Task<(IEnumerable<int> Ids, int TotalCount)> GetLikedSongIdsPaginatedAsync(int userId, int page, int pageSize, CancellationToken ct = default);
    Task<int> GetLikeCountAsync(int songId);
}
