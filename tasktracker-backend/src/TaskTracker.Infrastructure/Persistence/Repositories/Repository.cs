using Microsoft.EntityFrameworkCore;
using TaskTracker.Domain.Common;
using TaskTracker.Domain.Interfaces;

namespace TaskTracker.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic repository implementation over EF Core DbContext.
/// Concrete repositories inherit this and add domain-specific queries.
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T>    _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet   = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await _dbSet.ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await _dbSet.AddAsync(entity, ct);

    public virtual void Update(T entity)
        => _dbSet.Update(entity);

    public virtual void Remove(T entity)
        => _dbSet.Remove(entity);
}
