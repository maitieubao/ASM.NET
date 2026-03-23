using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface ISongService
{
    Task<IEnumerable<SongDto>> GetAllSongsAsync();
    Task<SongDto?> GetSongByIdAsync(int id);
    Task CreateSongAsync(SongDto songDto);
    Task ImportFromYoutubeAsync(string videoUrl);
    Task UpdateSongAsync(SongDto songDto);
    Task DeleteSongAsync(int id);
}
