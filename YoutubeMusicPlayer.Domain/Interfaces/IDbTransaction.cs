using System;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Domain.Interfaces;

public interface IDbTransaction : IDisposable, IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
