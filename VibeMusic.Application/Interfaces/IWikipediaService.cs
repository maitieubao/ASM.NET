using System.Threading.Tasks;

namespace VibeMusic.Application.Interfaces;

public interface IWikipediaService
{
    Task<string?> GetArtistBioAsync(string artistName);
    Task<string?> GetArtistImageAsync(string artistName);
    Task<string?> GetWikipediaUrlAsync(string artistName);
}
