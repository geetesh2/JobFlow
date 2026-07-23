using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Domain.Entities;
using JobFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JobFlow.Infrastructure.Persistence.Repositories;

public class EfJobRepository(ApplicationDbContext dbContext)
    : EfRepository<Job>(dbContext), IJobRepository
{
    public async Task<IReadOnlyList<Job>> GetByStatusAsync(
        JobStatus status, CancellationToken ct = default)
    {
        return await DbSet
            .Where(j => j.Status == status)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Job>> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        return await DbSet
            .OrderByDescending(j => j.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }
}
