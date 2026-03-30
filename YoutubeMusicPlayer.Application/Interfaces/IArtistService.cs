using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IArtistService
{
    Task<IEnumerable<ArtistDto>> GetAllArtistsAsync();
    Task<(IEnumerable<ArtistDto> Artists, int TotalCount)> GetPaginatedArtistsAsync(int page, int pageSize, string? searchTerm = null);
    Task<ArtistDto?> GetArtistByIdAsync(int id, int? currentUserId = null, int page = 1, int pageSize = 10);
    Task CreateArtistAsync(ArtistDto artistDto);
    Task UpdateArtistAsync(ArtistDto artistDto);
    Task DeleteArtistAsync(int id);
    Task<string?> RefreshArtistBioAsync(int id);
    Task<IEnumerable<ArtistDto>> GetFollowedArtistsAsync(int userId);
    Task<bool> ToggleFollowAsync(int userId, int artistId);
    Task<bool> IsFollowingAsync(int userId, int artistId);
    Task<string?> SyncWithDeezerAsync(int artistId);
}
