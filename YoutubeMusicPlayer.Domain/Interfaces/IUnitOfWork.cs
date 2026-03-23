using System;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<T> Repository<T>() where T : class;
    Task<int> CompleteAsync();
}
