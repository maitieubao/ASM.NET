using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => 
        await _context.Set<T>().FindAsync(new object[] { id }, ct);

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default) => 
        await _context.Set<T>().ToListAsync(ct);

    public IQueryable<T> Find(Expression<Func<T, bool>> expression) => 
        _context.Set<T>().Where(expression);

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression, CancellationToken ct = default) => 
        await _context.Set<T>().Where(expression).ToListAsync(ct);

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> expression, CancellationToken ct = default) => 
        await _context.Set<T>().FirstOrDefaultAsync(expression, ct);

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> expression, CancellationToken ct = default) => 
        await _context.Set<T>().AnyAsync(expression, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) => 
        await _context.Set<T>().AddAsync(entity, ct);

    public void Remove(T entity) => _context.Set<T>().Remove(entity);

    public void Update(T entity) => _context.Set<T>().Update(entity);
    
    public IQueryable<T> Query() => _context.Set<T>();
}
