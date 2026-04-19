using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Infrastructure.External.AiPlugins;

public class UserExperiencePlugin
{
    private readonly IUserService _userService;
    private readonly IRecommendationService _recommendationService;
    private readonly INotificationService _notificationService;
    private readonly ICommentService _commentService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPlaylistService _playlistService;
    private readonly ISongService _songService;
    private readonly IProfileFacade _profileFacade;
    private readonly IPlaybackFacade _playbackFacade;
    private readonly IHomeFacade _homeFacade;
    private readonly AiUserAccessGuard _userAccessGuard;

    public UserExperiencePlugin(
        IUserService userService,
        IRecommendationService recommendationService,
        INotificationService notificationService,
        ICommentService commentService,
        ISubscriptionService subscriptionService,
        IPlaylistService playlistService,
        ISongService songService,
        IProfileFacade profileFacade,
        IPlaybackFacade playbackFacade,
        IHomeFacade homeFacade,
        AiUserAccessGuard userAccessGuard)
    {
        _userService = userService;
        _recommendationService = recommendationService;
        _notificationService = notificationService;
        _commentService = commentService;
        _subscriptionService = subscriptionService;
        _playlistService = playlistService;
        _songService = songService;
        _profileFacade = profileFacade;
        _playbackFacade = playbackFacade;
        _homeFacade = homeFacade;
        _userAccessGuard = userAccessGuard;
    }

    [KernelFunction, Description("Lấy tóm tắt hồ sơ user và trạng thái premium.")]
    public async Task<string> GetUserProfileSummary(
        [Description("ID của người dùng")] int userId)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        var user = await _userService.GetUserByIdAsync(safeUserId);
        if (user == null) return "Không tìm thấy thông tin người dùng.";

        return $"User: {user.Username}\nEmail: {user.Email}\nPremium: {(user.IsPremium ? "Có" : "Không")}\nRole: {user.Role}\nLocked: {(user.IsLocked ? "Có" : "Không")}";
    }

    [KernelFunction, Description("Lấy hồ sơ chi tiết người dùng cho trang profile (playlist count, liked count, stats).")]
    public async Task<string> GetDetailedProfileOverview(
        [Description("ID của người dùng")] int userId)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        var profile = await _profileFacade.BuildUserProfileAsync(safeUserId);
        if (profile?.User == null) return "Không tìm thấy hồ sơ người dùng.";
        var sb = new StringBuilder();
        sb.AppendLine($"Hồ sơ của {profile.User.Username}");
        sb.AppendLine($"Premium: {(profile.User.IsPremium ? "Có" : "Không")}");
        sb.AppendLine($"Tổng playlist: {profile.Playlists.Count()}");
        sb.AppendLine($"Tổng bài đã thích: {profile.LikedSongsCount}");
        sb.AppendLine($"Tổng lượt nghe: {profile.ListeningHistory.Count()}");
        return sb.ToString().Trim();
    }

    [KernelFunction, Description("Lấy lịch sử nghe gần đây của user.")]
    public async Task<string> GetUserListeningHistorySummary(
        [Description("ID của người dùng")] int userId,
        [Description("Số bản ghi cần lấy")] int count = 10)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        count = ClampCount(count, 10, 30);

        var history = (await _userService.GetUserListeningHistoryAsync(safeUserId))
            .OrderByDescending(h => h.ListenedAt)
            .Take(count)
            .ToList();
        if (!history.Any()) return "Lịch sử nghe gần đây đang trống.";

        var sb = new StringBuilder();
        sb.AppendLine("Lịch sử nghe gần đây:");
        foreach (var item in history)
        {
            sb.AppendLine($"- {item.SongTitle} - {item.AuthorName} ({item.ListenedAt:yyyy-MM-dd HH:mm})");
        }
        return sb.ToString().Trim();
    }

    [KernelFunction, Description("Tạo playlist Daily Mix từ recommendation engine cho user.")]
    public async Task<string> CreateDailyMixPlaylist(
        [Description("ID của người dùng")] int userId,
        [Description("Tên playlist")] string playlistTitle = "Daily Mix của tôi",
        [Description("Số bài mong muốn")] int count = 12,
        [Description("Mô tả playlist")] string description = "Tạo tự động từ AI recommendation")
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        count = ClampCount(count, 12, 30);
        var safeTitle = NormalizeTitle(playlistTitle, "Daily Mix của tôi");

        var recs = (await _recommendationService.GetDailyMixAsync(safeUserId))
            .Where(r => !string.IsNullOrWhiteSpace(r.YoutubeVideoId))
            .Take(count)
            .ToList();
        if (!recs.Any()) return "Hiện chưa đủ dữ liệu để tạo Daily Mix.";

        var playlist = await _playlistService.CreatePlaylistAsync(safeUserId, safeTitle, description?.Trim());
        var addedTitles = new List<string>();

        foreach (var rec in recs)
        {
            var song = await _songService.GetOrCreateByYoutubeIdAsync(rec.YoutubeVideoId);
            if (song == null) continue;

            await _playlistService.AddSongToPlaylistAsync(playlist.PlaylistId, song.SongId, safeUserId);
            addedTitles.Add($"{song.Title} - {song.AuthorName}");
        }

        if (!addedTitles.Any())
        {
            return "Không thể ánh xạ recommendation sang bài hát nội bộ để tạo playlist.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Đã tạo playlist '{playlist.Title}' với {addedTitles.Count} bài từ Daily Mix.");
        sb.AppendLine("Một số bài nổi bật:");
        foreach (var t in addedTitles.Take(10))
        {
            sb.AppendLine($"- {t}");
        }
        sb.AppendLine($"ACTION:navigate:playlist:{playlist.PlaylistId}");
        return sb.ToString().Trim();
    }

    [KernelFunction, Description("Lấy các thông báo gần đây của user.")]
    public async Task<string> GetUserNotificationsSummary(
        [Description("ID của người dùng")] int userId,
        [Description("Số lượng thông báo")] int count = 10)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        count = ClampCount(count, 10, 30);

        var notifications = (await _notificationService.GetUserNotificationsAsync(safeUserId, count)).ToList();
        if (!notifications.Any()) return "Bạn không có thông báo mới.";

        return string.Join("\n", notifications.Select(n =>
            $"- [{(n.IsRead ? "Đã đọc" : "Chưa đọc")}] {n.Title}: {n.Message}"));
    }

    [KernelFunction, Description("Lấy trạng thái premium, gói đang có và lịch sử thanh toán gần đây của user.")]
    public async Task<string> GetSubscriptionSummary(
        [Description("ID của người dùng")] int userId,
        [Description("Số payment gần nhất")] int paymentCount = 5)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        paymentCount = ClampCount(paymentCount, 5, 20);

        var isPremium = await _subscriptionService.IsUserPremiumAsync(safeUserId);
        var payments = (await _subscriptionService.GetUserPaymentsAsync(safeUserId))
            .OrderByDescending(p => p.PaymentDate)
            .Take(paymentCount)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Trạng thái Premium: {(isPremium ? "Đang kích hoạt" : "Chưa kích hoạt")}");
        if (!payments.Any())
        {
            sb.AppendLine("Chưa có lịch sử thanh toán.");
            return sb.ToString().Trim();
        }

        sb.AppendLine("Thanh toán gần đây:");
        foreach (var p in payments)
        {
            sb.AppendLine($"- {p.PaymentDate:yyyy-MM-dd}: {p.PlanName} | {p.Amount} | {p.Status}");
        }
        return sb.ToString().Trim();
    }

    [KernelFunction, Description("Tạo bình luận mới cho một bài hát.")]
    public async Task<string> CreateSongComment(
        [Description("ID user")] int userId,
        [Description("ID bài hát")] int songId,
        [Description("Nội dung bình luận")] string content,
        [Description("ID bình luận cha nếu là reply")] int? parentId = null)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        if (string.IsNullOrWhiteSpace(content))
            return "Nội dung bình luận không được để trống.";

        var comment = await _commentService.CreateCommentAsync(safeUserId, songId, content.Trim(), parentId);
        return $"Đã đăng bình luận thành công (CommentId: {comment.CommentId}) cho bài hát ID {songId}.";
    }

    [KernelFunction, Description("Lấy danh sách bình luận của bài hát.")]
    public async Task<string> GetSongCommentsSummary(
        [Description("ID bài hát")] int songId,
        [Description("ID user hiện tại (optional)")] int? currentUserId = null,
        [Description("Trang")] int page = 1,
        [Description("Số lượng mỗi trang")] int pageSize = 5)
    {
        var safeCurrentUserId = _userAccessGuard.EnsureAuthorizedOrUseCurrent(currentUserId);
        if (page < 1) page = 1;
        pageSize = ClampCount(pageSize, 5, 20);

        var (comments, total) = await _commentService.GetSongCommentsPaginatedAsync(songId, safeCurrentUserId, page, pageSize);
        var commentList = comments.ToList();
        if (!commentList.Any()) return "Bài hát này chưa có bình luận.";

        var sb = new StringBuilder();
        sb.AppendLine($"Tổng bình luận: {total}");
        foreach (var c in commentList)
        {
            sb.AppendLine($"- {c.UserName}: {c.Content}");
        }
        return sb.ToString().Trim();
    }

    [KernelFunction, Description("Lấy stream trực tiếp theo videoId YouTube để hỗ trợ phát nhạc.")]
    public async Task<string> ResolvePlaybackStreamByVideoId(
        [Description("YouTube videoId")] string videoId,
        [Description("ID user (optional)")] int? userId = null)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedOrUseCurrent(userId);
        if (string.IsNullOrWhiteSpace(videoId))
            return "videoId không hợp lệ.";

        var stream = await _playbackFacade.GetStreamAsync($"https://youtube.com/watch?v={videoId.Trim()}", null, null, safeUserId);
        if (!string.IsNullOrWhiteSpace(stream.Error))
            return $"Không thể lấy stream: {stream.Error}";
        if (string.IsNullOrWhiteSpace(stream.StreamUrl))
            return "Không thể lấy stream tại thời điểm hiện tại.";
        return $"Đã resolve stream: {stream.StreamUrl}";
    }

    [KernelFunction, Description("Lấy gợi ý nhanh một section trang chủ cho người dùng.")]
    public async Task<string> GetHomeSectionSummary(
        [Description("Loại section: daily-mix, because-you-listened, chill-mood...")] string type,
        [Description("ID user (optional)")] int? userId = null,
        [Description("Refresh cache hay không")] bool refresh = false)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedOrUseCurrent(userId);
        if (string.IsNullOrWhiteSpace(type))
            return "Thiếu loại section cần lấy.";

        var section = await _homeFacade.GetHomeSectionAsync(type.Trim(), safeUserId, refresh);
        if (section == null) return $"Không tìm thấy section '{type}'.";

        var songLines = section.Songs?.Take(8).Select(s => $"- {s.Title} - {s.AuthorName}") ?? Enumerable.Empty<string>();
        return $"Section: {section.Title}\n{string.Join("\n", songLines)}";
    }

    private static int ClampCount(int value, int defaultValue, int maxValue)
    {
        if (value <= 0) return defaultValue;
        return Math.Min(value, maxValue);
    }

    private static string NormalizeTitle(string? value, string fallback)
    {
        var safe = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return safe.Length > 120 ? safe[..120] : safe;
    }
}
