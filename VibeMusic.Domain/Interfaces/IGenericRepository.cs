using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Domain.Interfaces;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    IQueryable<T> Find(Expression<Func<T, bool>> expression);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> expression, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> expression, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Remove(T entity);
    void Update(T entity);
    IQueryable<T> Query();
}
