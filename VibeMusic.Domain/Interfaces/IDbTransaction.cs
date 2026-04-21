using System;
using System.Threading.Tasks;

namespace VibeMusic.Domain.Interfaces;

public interface IDbTransaction : IDisposable, IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
