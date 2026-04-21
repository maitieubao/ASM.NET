using VibeMusic.Application.DTOs;
using VibeMusic.Domain.Entities;
namespace VibeMusic.Application.Interfaces;
public interface IExternalViewCountSyncService
{
    Task<SyncResult> SyncSongAsync(int songId, ViewCountSource source, CancellationToken ct = default);
    Task<IEnumerable<SyncResult>> SyncAllSourcesAsync(int songId, CancellationToken ct = default);
}
