using System.Threading;
using System.Collections;
using YoutubeMusicPlayer.Domain.Interfaces;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace YoutubeMusicPlayer.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private Hashtable? _repositories;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IGenericRepository<T> Repository<T>() where T : class
    {
        if (_repositories == null) _repositories = new Hashtable();

        var type = typeof(T);

        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(GenericRepository<>);
            var repositoryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(T)), _context);
            _repositories.Add(type, repositoryInstance);
        }

        return (IGenericRepository<T>)_repositories[type]!;
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
