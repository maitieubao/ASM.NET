using Microsoft.SemanticKernel;
using System.ComponentModel;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using System.Text;
using System;
using System.Linq;
using System.Collections.Generic;

namespace YoutubeMusicPlayer.Infrastructure.External.AiPlugins;

public class PlaylistPlugin
{
    private readonly IPlaylistService _playlistService;
    private readonly ISongService _songService;
    private readonly IInteractionService _interactionService;
    private readonly AiUserAccessGuard _userAccessGuard;

    public PlaylistPlugin(
        IPlaylistService playlistService,
        ISongService songService,
        IInteractionService interactionService,
        AiUserAccessGuard userAccessGuard)
    {
        _playlistService = playlistService;
        _songService = songService;
        _interactionService = interactionService;
        _userAccessGuard = userAccessGuard;
    }

    [KernelFunction, Description("Lấy danh sách tất cả các bài hát trong một playlist.")]
    public async Task<string> GetPlaylistSongs(
        [Description("ID của playlist")] int playlistId,
        [Description("ID của người dùng")] int userId)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        var playlist = await _playlistService.GetPlaylistByIdAsync(playlistId, safeUserId);
        if (playlist == null || !playlist.Songs.Any()) return "Playlist này hiện đang trống hoặc không tồn tại.";

        return string.Join("\n", playlist.Songs.Select(s => $"- {s.Title} by {s.AuthorName} (ID: {s.SongId})"));
    }

    [KernelFunction, Description("Tạo một playlist mới cho người dùng.")]
    public async Task<string> CreatePlaylist(
        [Description("ID của người dùng")] int userId,
        [Description("Tên của playlist mới")] string title,
        [Description("Mô tả về playlist")] string description = "")
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        var safeTitle = NormalizePlaylistTitle(title);
        await _playlistService.CreatePlaylistAsync(safeUserId, safeTitle, description?.Trim());
        return $"Đã tạo playlist '{safeTitle}' thành công.";
    }

    [KernelFunction, Description("Thêm một bài hát vào playlist.")]
    public async Task<string> AddSongToPlaylist(
        [Description("ID của playlist")] int playlistId,
        [Description("ID của bài hát")] int songId,
        [Description("ID của người dùng")] int userId)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        await _playlistService.AddSongToPlaylistAsync(playlistId, songId, safeUserId);
        return "Đã thêm bài hát vào playlist.";
    }

    [KernelFunction, Description("Xóa một bài hát khỏi playlist.")]
    public async Task<string> RemoveSongFromPlaylist(
        [Description("ID của playlist")] int playlistId,
        [Description("ID của bài hát")] int songId,
        [Description("ID của người dùng")] int userId)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        await _playlistService.RemoveSongFromPlaylistAsync(playlistId, songId, safeUserId);
        return "Đã xóa bài hát khỏi playlist.";
    }

    [KernelFunction, Description("Lấy danh sách tất cả các playlist của người dùng.")]
    public async Task<string> GetUserPlaylists(
        [Description("ID của người dùng")] int userId)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        var playlists = await _playlistService.GetUserPlaylistsAsync(safeUserId);
        if (playlists == null || !playlists.Any()) return "Bạn chưa có playlist nào.";

        return string.Join("\n", playlists.Select(p => $"- {p.Title} (ID: {p.PlaylistId})"));
    }

    [KernelFunction, Description("Cập nhật thông tin playlist (tên, mô tả, quyền riêng tư).")]
    public async Task<string> UpdatePlaylistInfo(
        [Description("ID người dùng")] int userId,
        [Description("ID playlist")] int playlistId,
        [Description("Tên playlist mới")] string title,
        [Description("Mô tả mới")] string description = "",
        [Description("Visibility: Public hoặc Private")] string visibility = "Public")
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        var playlist = await _playlistService.GetPlaylistByIdAsync(playlistId, safeUserId);
        if (playlist == null) return "Không tìm thấy playlist cần cập nhật.";

        playlist.Title = string.IsNullOrWhiteSpace(title) ? playlist.Title : NormalizePlaylistTitle(title);
        playlist.Description = description?.Trim();
        playlist.Visibility = string.Equals(visibility, "Private", StringComparison.OrdinalIgnoreCase)
            ? "Private"
            : "Public";

        await _playlistService.UpdatePlaylistAsync(playlist, safeUserId);
        return $"Đã cập nhật playlist '{playlist.Title}' (ID: {playlist.PlaylistId}).";
    }

    [KernelFunction, Description("Xóa playlist của người dùng.")]
    public async Task<string> DeletePlaylist(
        [Description("ID người dùng")] int userId,
        [Description("ID playlist")] int playlistId)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        await _playlistService.DeletePlaylistAsync(playlistId, safeUserId);
        return $"Đã xóa playlist ID {playlistId}.";
    }

    [KernelFunction, Description("Tạo playlist mới từ N bài hát người dùng vừa bấm Like gần nhất.")]
    public async Task<string> CreatePlaylistFromRecentLikedSongs(
        [Description("ID của người dùng")] int userId,
        [Description("Tên playlist mới")] string playlistTitle,
        [Description("Số bài lấy từ like gần nhất")] int count = 10,
        [Description("Mô tả playlist")] string description = "Tạo tự động từ bài hát yêu thích gần đây")
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        count = ClampCount(count, 10, 50);
        var safeTitle = NormalizePlaylistTitle(playlistTitle);

        var likedSongIds = (await _interactionService.GetLikedSongIdsAsync(safeUserId))
            .Take(count)
            .ToList();
        if (!likedSongIds.Any()) return "Bạn chưa có bài hát yêu thích gần đây để tạo playlist.";

        var newPlaylist = await _playlistService.CreatePlaylistAsync(safeUserId, safeTitle, description?.Trim());
        foreach (var songId in likedSongIds)
        {
            await _playlistService.AddSongToPlaylistAsync(newPlaylist.PlaylistId, songId, safeUserId);
        }

        var songs = (await _songService.GetSongsByIdsAsync(likedSongIds)).ToList();
        return BuildPlaylistCreationResult(
            newPlaylist.PlaylistId,
            songs,
            $"Đã tạo playlist '{newPlaylist.Title}' từ {likedSongIds.Count} bài bạn vừa Like.");
    }

    [KernelFunction, Description("Tạo playlist mới từ N bài hát người dùng nghe gần đây, có thể lọc theo nghệ sĩ.")]
    public async Task<string> CreatePlaylistFromRecentListeningHistory(
        [Description("ID của người dùng")] int userId,
        [Description("Tên playlist mới")] string playlistTitle,
        [Description("Số bài lấy từ lịch sử nghe")] int count = 10,
        [Description("Tên nghệ sĩ để lọc, ví dụ 'Ed Sheeran'. Để trống nếu không lọc")] string artistName = "",
        [Description("Mô tả playlist")] string description = "Tạo tự động từ lịch sử nghe gần đây")
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        count = ClampCount(count, 10, 50);
        var safeTitle = NormalizePlaylistTitle(playlistTitle);

        var historySongIds = (await _interactionService.GetRecentListeningHistoryAsync(safeUserId, Math.Max(count * 4, 20)))
            .ToList();
        if (!historySongIds.Any()) return "Lịch sử nghe nhạc của bạn đang trống.";

        var historySongs = (await _songService.GetSongsByIdsAsync(historySongIds)).ToList();
        var selectedSongs = string.IsNullOrWhiteSpace(artistName)
            ? historySongs.Take(count).ToList()
            : historySongs
                .Where(s => !string.IsNullOrWhiteSpace(s.AuthorName) &&
                            s.AuthorName.Contains(artistName, StringComparison.OrdinalIgnoreCase))
                .Take(count)
                .ToList();

        if (!selectedSongs.Any())
        {
            return string.IsNullOrWhiteSpace(artistName)
                ? "Không đủ dữ liệu lịch sử nghe để tạo playlist."
                : $"Không tìm thấy bài nghe gần đây của nghệ sĩ '{artistName}'.";
        }

        var newPlaylist = await _playlistService.CreatePlaylistAsync(safeUserId, safeTitle, description?.Trim());
        foreach (var song in selectedSongs)
        {
            await _playlistService.AddSongToPlaylistAsync(newPlaylist.PlaylistId, song.SongId, safeUserId);
        }

        var prefix = string.IsNullOrWhiteSpace(artistName)
            ? $"Đã tạo playlist '{newPlaylist.Title}' từ {selectedSongs.Count} bài nghe gần nhất."
            : $"Đã tạo playlist '{newPlaylist.Title}' gồm {selectedSongs.Count} bài nghe gần nhất của '{artistName}'.";

        return BuildPlaylistCreationResult(newPlaylist.PlaylistId, selectedSongs, prefix);
    }

    [KernelFunction, Description("Thêm N bài hát người dùng vừa Like gần đây vào một playlist đã có.")]
    public async Task<string> AddRecentLikedSongsToPlaylist(
        [Description("ID playlist cần thêm bài")] int playlistId,
        [Description("ID của người dùng")] int userId,
        [Description("Số bài like gần nhất cần thêm")] int count = 10)
    {
        var safeUserId = _userAccessGuard.EnsureAuthorizedUser(userId);
        count = ClampCount(count, 10, 50);

        var likedSongIds = (await _interactionService.GetLikedSongIdsAsync(safeUserId))
            .Take(count)
            .ToList();
        if (!likedSongIds.Any()) return "Bạn chưa có bài hát yêu thích gần đây để thêm vào playlist.";

        foreach (var songId in likedSongIds)
        {
            await _playlistService.AddSongToPlaylistAsync(playlistId, songId, safeUserId);
        }

        var songs = (await _songService.GetSongsByIdsAsync(likedSongIds)).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"Đã thêm tối đa {likedSongIds.Count} bài like gần nhất vào playlist ID {playlistId}.");
        sb.AppendLine("Một số bài vừa thêm:");
        foreach (var song in songs.Take(8))
        {
            sb.AppendLine($"- {song.Title} - {song.AuthorName}");
        }
        return sb.ToString().Trim();
    }

    private static string BuildPlaylistCreationResult(int playlistId, List<SongDto> songs, string intro)
    {
        var sb = new StringBuilder();
        sb.AppendLine(intro);
        sb.AppendLine($"Playlist ID: {playlistId}");
        sb.AppendLine("Danh sách bài hát:");
        foreach (var song in songs.Take(20))
        {
            sb.AppendLine($"- {song.Title} - {song.AuthorName}");
        }

        // Frontend currently supports navigate action via SuggestedAction.
        sb.AppendLine($"ACTION:navigate:playlist:{playlistId}");
        return sb.ToString().Trim();
    }

    private static int ClampCount(int value, int defaultValue, int maxValue)
    {
        if (value <= 0) return defaultValue;
        return Math.Min(value, maxValue);
    }

    private static string NormalizePlaylistTitle(string? title)
    {
        var safeTitle = string.IsNullOrWhiteSpace(title) ? "Playlist mới" : title.Trim();
        return safeTitle.Length > 120 ? safeTitle[..120] : safeTitle;
    }
}
