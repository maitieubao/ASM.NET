using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IArtistService
{
    Task<IEnumerable<ArtistDto>> GetAllArtistsAsync();
    Task<ArtistDto?> GetArtistByIdAsync(int id, int page = 1, int pageSize = 10);
    Task CreateArtistAsync(ArtistDto artistDto);
    Task UpdateArtistAsync(ArtistDto artistDto);
    Task DeleteArtistAsync(int id);
    Task<string?> RefreshArtistBioAsync(int id);
}
