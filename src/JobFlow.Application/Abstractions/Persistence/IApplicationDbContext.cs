using JobFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobFlow.Application.Abstractions.Persistence;

public interface IApplicationDbContext
{
    DbSet<Job> Jobs { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
