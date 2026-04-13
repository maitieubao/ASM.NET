using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IArtistService
{
    Task<IEnumerable<ArtistDto>> GetAllArtistsAsync(CancellationToken ct = default);
    Task<(IEnumerable<ArtistDto> Artists, int TotalCount)> GetPaginatedArtistsAsync(int page, int pageSize, string? searchTerm = null, CancellationToken ct = default);
    Task<ArtistDto?> GetArtistByIdAsync(int id, int? currentUserId = null, int page = 1, int pageSize = 10, CancellationToken ct = default);
    Task CreateArtistAsync(ArtistDto artistDto, CancellationToken ct = default);
    Task UpdateArtistAsync(ArtistDto artistDto, CancellationToken ct = default);
    Task DeleteArtistAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<ArtistDto>> SearchArtistsAsync(string term, int count, CancellationToken ct = default);
    Task<bool> ToggleVerifiedStatusAsync(int id, CancellationToken ct = default);
    Task<string?> RefreshArtistBioAsync(int id, CancellationToken ct = default);
    Task<string?> SyncArtistMetadataAsync(int artistId, CancellationToken ct = default);
    Task<IEnumerable<ArtistDto>> GetFollowedArtistsAsync(int userId, CancellationToken ct = default);
    Task<bool> ToggleFollowAsync(int userId, int artistId, CancellationToken ct = default);
    Task<bool> IsFollowingAsync(int userId, int artistId, CancellationToken ct = default);
    Task<string?> SyncWithDeezerAsync(int artistId, CancellationToken ct = default);
    Task<IEnumerable<ArtistDto>> SearchArtistsAsync(string query, CancellationToken ct = default);
    Task<ArtistDto> GetOrCreateArtistStubAsync(string name, string? avatarUrl = null, CancellationToken ct = default);
}
