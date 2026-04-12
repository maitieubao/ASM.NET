using System.Collections.Concurrent;
using YoutubeMusicPlayer.Domain.Interfaces;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace YoutubeMusicPlayer.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        _repositories = new ConcurrentDictionary<Type, object>();
    }

    public IGenericRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);

        return (IGenericRepository<T>)_repositories.GetOrAdd(type, t =>
        {
            var repositoryType = typeof(GenericRepository<>);
            return Activator.CreateInstance(repositoryType.MakeGenericType(typeof(T)), _context)!;
        });
    }

    public async Task<int> CompleteAsync(CancellationToken ct = default) => 
        await _context.SaveChangesAsync(ct);

    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new DbContextTransactionWrapper(transaction);
    }

    public async Task<int> ExecuteSqlRawAsync(string sql, CancellationToken ct = default, params object[] parameters) => 
        await _context.Database.ExecuteSqlRawAsync(sql, parameters, ct);

    public void Dispose() => _context.Dispose();
}
