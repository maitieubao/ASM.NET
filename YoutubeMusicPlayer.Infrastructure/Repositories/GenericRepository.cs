using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Domain.Interfaces;
using YoutubeMusicPlayer.Infrastructure.Persistence;

namespace YoutubeMusicPlayer.Infrastructure.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    public GenericRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<T?> GetByIdAsync(int id) => await _context.Set<T>().FindAsync(id);

    public async Task<IEnumerable<T>> GetAllAsync() => await _context.Set<T>().ToListAsync();

    public IEnumerable<T> Find(Expression<Func<T, bool>> expression) => _context.Set<T>().Where(expression);

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> expression) => await _context.Set<T>().FirstOrDefaultAsync(expression);

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> expression) => await _context.Set<T>().AnyAsync(expression);

    public async Task AddAsync(T entity) => await _context.Set<T>().AddAsync(entity);

    public void Remove(T entity) => _context.Set<T>().Remove(entity);

    public void Update(T entity) => _context.Set<T>().Update(entity);
}
