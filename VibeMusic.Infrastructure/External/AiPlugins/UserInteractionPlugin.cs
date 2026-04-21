using Microsoft.SemanticKernel;
using System.ComponentModel;
using VibeMusic.Application.Interfaces;

namespace VibeMusic.Infrastructure.External.AiPlugins;

public class UserInteractionPlugin
{
    private readonly IInteractionService _interactionService;
    private readonly ISongService _songService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly AiUserAccessGuard _userAccessGuard;

    public UserInteractionPlugin(
        IInteractionService interactionService,
        ISongService songService,
        ISubscriptionService subscriptionService,
        AiUserAccessGuard userAccessGuard)
    {
        _interactionService = interactionService;
        _songService = songService;
        _subscriptionService = subscriptionService;
        _userAccessGuard = userAccessGuard;
    }

    [KernelFunction, Description("Lấy lịch sử nghe nhạc gần đây của người dùng.")]
    public async Task<string> GetUserListeningHistory(
        [Description("ID của người dùng")] int userId,
        [Description("Số lượng bài hát cần lấy")] int count = 5)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        var historyIds = await _interactionService.GetRecentListeningHistoryAsync(safeUserId, count);
        if (historyIds == null || !historyIds.Any()) return "Lịch sử nghe nhạc hiện đang trống.";

        var songs = await _songService.GetSongsByIdsAsync(historyIds);
        return string.Join("\n", songs.Select(s => $"- {s.Title} by {s.AuthorName}"));
    }

    [KernelFunction, Description("Lấy danh sách các bài hát người dùng đã yêu thích.")]
    public async Task<string> GetUserLikedSongs(
        [Description("ID của người dùng")] int userId)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        var likedIds = await _interactionService.GetLikedSongIdsAsync(safeUserId);
        if (likedIds == null || !likedIds.Any()) return "Người dùng chưa thích bài hát nào.";

        var songs = await _songService.GetSongsByIdsAsync(likedIds);
        return string.Join("\n", songs.Select(s => $"- {s.Title} by {s.AuthorName}"));
    }

    [KernelFunction, Description("Đánh dấu bài hát là yêu thích hoặc hủy yêu thích.")]
    public async Task<string> ToggleLikeSong(
        [Description("ID của người dùng")] int userId,
        [Description("ID của bài hát")] int songId)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        var result = await _interactionService.ToggleLikeAsync(safeUserId, songId);
        return result ? "Đã thêm vào danh sách yêu thích." : "Đã hủy yêu thích.";
    }

    [KernelFunction, Description("Kiểm tra xem người dùng có phải là thành viên Premium hay không.")]
    public async Task<bool> IsUserPremium(
        [Description("ID của người dùng")] int userId)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        return await _subscriptionService.IsUserPremiumAsync(safeUserId);
    }
}
