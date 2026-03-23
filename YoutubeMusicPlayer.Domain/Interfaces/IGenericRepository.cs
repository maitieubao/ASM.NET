using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Domain.Interfaces;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    IEnumerable<T> Find(Expression<Func<T, bool>> expression);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> expression);
    Task<bool> AnyAsync(Expression<Func<T, bool>> expression);
    Task AddAsync(T entity);
    void Remove(T entity);
    void Update(T entity);
}
