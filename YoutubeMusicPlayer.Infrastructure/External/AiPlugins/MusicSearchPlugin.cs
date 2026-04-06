using Microsoft.SemanticKernel;
using System.ComponentModel;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Infrastructure.External.AiPlugins;

public class MusicSearchPlugin
{
    private readonly IYoutubeService _youtubeService;
    private readonly IDeezerService _deezerService;

    public MusicSearchPlugin(IYoutubeService youtubeService, IDeezerService deezerService)
    {
        _youtubeService = youtubeService;
        _deezerService = deezerService;
    }

    [KernelFunction, Description("Tìm kiếm nhạc trên YouTube và Deezer. Trả về danh sách bài hát kèm ID để phát.")]
    public async Task<string> SearchMusic(
        [Description("Từ khóa tìm kiếm (tên bài hát, nghệ sĩ)")] string query)
    {
        var results = await _youtubeService.SearchVideosAsync(query, 5);
        if (!results.Any()) return "Không tìm thấy kết quả nào cho '" + query + "'.";

        var list = results.Select(r => $"* {r.Title} - {r.AuthorName} [ID: {r.YoutubeVideoId}]");
        return "Kết quả tìm kiếm:\n" + string.Join("\n", list) + "\n\nĐể phát một bài, hãy sử dụng ID tương ứng.";
    }

    [KernelFunction, Description("Lấy thông tin chi tiết về một bài hát hoặc video YouTube.")]
    public async Task<string> GetMusicDetails(
        [Description("ID của video YouTube")] string videoId)
    {
        var details = await _youtubeService.GetVideoDetailsAsync($"https://youtube.com/watch?v={videoId}");
        return $"Tiêu đề: {details.Title}\nNghệ sĩ: {details.AuthorName}\nThời lượng: {details.Duration}\nThể loại: {details.Genre}";
    }
}
