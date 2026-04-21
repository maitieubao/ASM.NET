using VibeMusic.Domain.Interfaces;

namespace VibeMusic.Infrastructure;

internal sealed class NoOpDbTransaction : IDbTransaction
{
    public static NoOpDbTransaction Instance { get; } = new();

    private NoOpDbTransaction()
    {
    }

    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
