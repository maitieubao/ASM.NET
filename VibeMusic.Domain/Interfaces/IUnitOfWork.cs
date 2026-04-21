using System.Threading;

namespace VibeMusic.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<T> Repository<T>() where T : class;
    Task<int> CompleteAsync(CancellationToken ct = default);
    Task<IDbTransaction> BeginTransactionAsync(CancellationToken ct = default); // New: Database transaction support
    Task<int> ExecuteSqlRawAsync(string sql, CancellationToken ct = default, params object[] parameters);
}
