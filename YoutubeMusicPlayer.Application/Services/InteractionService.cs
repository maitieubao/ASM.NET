using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class InteractionService : IInteractionService
{
    private readonly IUnitOfWork _unitOfWork;

    public InteractionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<int>> GetRecentListeningHistoryAsync(int userId, int count = 20)
    {
        var history = _unitOfWork.Repository<ListeningHistory>()
            .Find(lh => lh.UserId == userId)
            .OrderByDescending(lh => lh.ListenedAt)
            .Select(lh => lh.SongId)
            .Distinct()
            .Take(count)
            .ToList();
            
        return await Task.FromResult(history);
    }

    public async Task<IEnumerable<string>> GetRecentSearchHistoryAsync(int userId, int count = 10)
    {
        var searches = _unitOfWork.Repository<UserSearchHistory>()
            .Find(sh => sh.UserId == userId)
            .OrderByDescending(sh => sh.SearchedAt)
            .Select(sh => sh.SearchQuery)
            .Distinct()
            .Take(count)
            .ToList();
            
        return await Task.FromResult(searches);
    }

    public async Task RecordListeningHistoryAsync(int userId, int songId)
    {
        var history = new ListeningHistory
        {
            UserId = userId,
            SongId = songId,
            ListenedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<ListeningHistory>().AddAsync(history);
        await _unitOfWork.CompleteAsync();
    }

    public async Task RecordSearchHistoryAsync(int userId, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        var history = new UserSearchHistory
        {
            UserId = userId,
            SearchQuery = query.Trim(),
            SearchedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<UserSearchHistory>().AddAsync(history);
        await _unitOfWork.CompleteAsync();
    }
}
