using System;
using System.Collections;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Domain.Interfaces;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure.Repositories;

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

        var type = typeof(T).Namespace + "." + typeof(T).Name;

        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(GenericRepository<>);
            var repositoryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(T)), _context);
            _repositories.Add(type, repositoryInstance);
        }

        return (IGenericRepository<T>)_repositories[type]!;
    }

    public async Task<int> CompleteAsync() => await _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}
