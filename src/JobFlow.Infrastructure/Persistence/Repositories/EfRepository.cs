using JobFlow.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobFlow.Infrastructure.Persistence.Repositories;

public class EfRepository<T>(ApplicationDbContext dbContext) : IRepository<T>
    where T : class
{
    protected readonly ApplicationDbContext DbContext = dbContext;
    protected readonly DbSet<T> DbSet = dbContext.Set<T>();

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbSet.ToListAsync(ct);
    }

    public async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await DbSet.AddAsync(entity, ct);
    }

    public void Update(T entity)
    {
        DbSet.Update(entity);
    }

    public void Delete(T entity)
    {
        DbSet.Remove(entity);
    }
}
