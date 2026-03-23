using System.Collections.Generic;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IInteractionService
{
    Task RecordListeningHistoryAsync(int userId, int songId);
    Task<IEnumerable<int>> GetRecentListeningHistoryAsync(int userId, int count = 20);
    Task RecordSearchHistoryAsync(int userId, string query);
    Task<IEnumerable<string>> GetRecentSearchHistoryAsync(int userId, int count = 10);
}
