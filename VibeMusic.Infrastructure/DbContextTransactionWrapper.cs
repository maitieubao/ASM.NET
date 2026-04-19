using Microsoft.EntityFrameworkCore.Storage;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Infrastructure;

public class DbContextTransactionWrapper : IDbTransaction
{
    private readonly IDbContextTransaction _transaction;

    public DbContextTransactionWrapper(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task CommitAsync(CancellationToken ct = default) => await _transaction.CommitAsync(ct);

    public async Task RollbackAsync(CancellationToken ct = default) => await _transaction.RollbackAsync(ct);

    public void Dispose() => _transaction.Dispose();

    public async ValueTask DisposeAsync() => await _transaction.DisposeAsync();
}
