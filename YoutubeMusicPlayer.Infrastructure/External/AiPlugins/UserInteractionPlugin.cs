using Microsoft.SemanticKernel;
using System.ComponentModel;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Infrastructure.External.AiPlugins;

public class UserInteractionPlugin
{
    private readonly IInteractionService _interactionService;
    private readonly ISongService _songService;
    private readonly ISubscriptionService _subscriptionService;

    public UserInteractionPlugin(IInteractionService interactionService, ISongService songService, ISubscriptionService subscriptionService)
    {
        _interactionService = interactionService;
        _songService = songService;
        _subscriptionService = subscriptionService;
    }

    [KernelFunction, Description("Lấy lịch sử nghe nhạc gần đây của người dùng.")]
    public async Task<string> GetUserListeningHistory(
        [Description("ID của người dùng")] int userId,
        [Description("Số lượng bài hát cần lấy")] int count = 5)
    {
        var historyIds = await _interactionService.GetRecentListeningHistoryAsync(userId, count);
        if (historyIds == null || !historyIds.Any()) return "Lịch sử nghe nhạc hiện đang trống.";

        var songs = await _songService.GetSongsByIdsAsync(historyIds);
        return string.Join("\n", songs.Select(s => $"- {s.Title} by {s.AuthorName}"));
    }

    [KernelFunction, Description("Lấy danh sách các bài hát người dùng đã yêu thích.")]
    public async Task<string> GetUserLikedSongs(
        [Description("ID của người dùng")] int userId)
    {
        var likedIds = await _interactionService.GetLikedSongIdsAsync(userId);
        if (likedIds == null || !likedIds.Any()) return "Người dùng chưa thích bài hát nào.";

        var songs = await _songService.GetSongsByIdsAsync(likedIds);
        return string.Join("\n", songs.Select(s => $"- {s.Title} by {s.AuthorName}"));
    }

    [KernelFunction, Description("Đánh dấu bài hát là yêu thích hoặc hủy yêu thích.")]
    public async Task<string> ToggleLikeSong(
        [Description("ID của người dùng")] int userId,
        [Description("ID của bài hát")] int songId)
    {
        var result = await _interactionService.ToggleLikeAsync(userId, songId);
        return result ? "Đã thêm vào danh sách yêu thích." : "Đã hủy yêu thích.";
    }

    [KernelFunction, Description("Kiểm tra xem người dùng có phải là thành viên Premium hay không.")]
    public async Task<bool> IsUserPremium(
        [Description("ID của người dùng")] int userId)
    {
        return await _subscriptionService.IsUserPremiumAsync(userId);
    }
}
