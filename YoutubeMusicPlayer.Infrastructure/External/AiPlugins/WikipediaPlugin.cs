using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Infrastructure.External.AiPlugins;

public class WikipediaPlugin
{
    private readonly IWikipediaService _wikipediaService;

    public WikipediaPlugin(IWikipediaService wikipediaService)
    {
        _wikipediaService = wikipediaService;
    }

    [KernelFunction, Description("Lấy tiểu sử, thông tin chi tiết về một nghệ sĩ hoặc ban nhạc từ Wikipedia.")]
    public async Task<string> GetArtistBiography(
        [Description("Tên nghệ sĩ hoặc nhóm nhạc để tra cứu")] string artistName)
    {
        var bio = await _wikipediaService.GetArtistBioAsync(artistName);
        if (string.IsNullOrEmpty(bio)) return $"Không tìm thấy thông tin tiểu sử cho nghệ sĩ '{artistName}' trên Wikipedia.";

        return $"Tiểu sử của {artistName}:\n{bio}";
    }
}
