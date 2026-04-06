using Microsoft.SemanticKernel;
using System.ComponentModel;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Infrastructure.External.AiPlugins;

public class PlaylistPlugin
{
    private readonly IPlaylistService _playlistService;
    private readonly ISongService _songService;

    public PlaylistPlugin(IPlaylistService playlistService, ISongService songService)
    {
        _playlistService = playlistService;
        _songService = songService;
    }

    [KernelFunction, Description("Lấy danh sách tất cả các bài hát trong một playlist.")]
    public async Task<string> GetPlaylistSongs(
        [Description("ID của playlist")] int playlistId)
    {
        var playlist = await _playlistService.GetPlaylistByIdAsync(playlistId);
        if (playlist == null || !playlist.Songs.Any()) return "Playlist này hiện đang trống hoặc không tồn tại.";

        return string.Join("\n", playlist.Songs.Select(s => $"- {s.Title} by {s.AuthorName} (ID: {s.SongId})"));
    }

    [KernelFunction, Description("Tạo một playlist mới cho người dùng.")]
    public async Task<string> CreatePlaylist(
        [Description("ID của người dùng")] int userId,
        [Description("Tên của playlist mới")] string title,
        [Description("Mô tả về playlist")] string description = "")
    {
        await _playlistService.CreatePlaylistAsync(userId, title, description);
        return $"Đã tạo playlist '{title}' thành công.";
    }

    [KernelFunction, Description("Thêm một bài hát vào playlist.")]
    public async Task<string> AddSongToPlaylist(
        [Description("ID của playlist")] int playlistId,
        [Description("ID của bài hát")] int songId,
        [Description("ID của người dùng")] int userId)
    {
        await _playlistService.AddSongToPlaylistAsync(playlistId, songId, userId);
        return "Đã thêm bài hát vào playlist.";
    }

    [KernelFunction, Description("Xóa một bài hát khỏi playlist.")]
    public async Task<string> RemoveSongFromPlaylist(
        [Description("ID của playlist")] int playlistId,
        [Description("ID của bài hát")] int songId,
        [Description("ID của người dùng")] int userId)
    {
        await _playlistService.RemoveSongFromPlaylistAsync(playlistId, songId, userId);
        return "Đã xóa bài hát khỏi playlist.";
    }

    [KernelFunction, Description("Lấy danh sách tất cả các playlist của người dùng.")]
    public async Task<string> GetUserPlaylists(
        [Description("ID của người dùng")] int userId)
    {
        var playlists = await _playlistService.GetUserPlaylistsAsync(userId);
        if (playlists == null || !playlists.Any()) return "Bạn chưa có playlist nào.";

        return string.Join("\n", playlists.Select(p => $"- {p.Title} (ID: {p.PlaylistId})"));
    }
}
