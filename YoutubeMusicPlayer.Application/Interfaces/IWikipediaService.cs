using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IWikipediaService
{
    Task<string?> GetArtistBioAsync(string artistName);
    Task<string?> GetArtistImageAsync(string artistName);
}
