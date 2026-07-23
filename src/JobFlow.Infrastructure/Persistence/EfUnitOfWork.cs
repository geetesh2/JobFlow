using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Domain.Common;
using JobFlow.Infrastructure.Persistence.Repositories;
using MediatR;

namespace JobFlow.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPublisher _publisher;
    private IJobRepository? _jobs;

    public EfUnitOfWork(ApplicationDbContext dbContext, IPublisher publisher)
    {
        _dbContext = dbContext;
        _publisher = publisher;
    }

    public IJobRepository Jobs => _jobs ??= new EfJobRepository(_dbContext);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var result = await _dbContext.SaveChangesAsync(ct);

        var entities = _dbContext.ChangeTracker
            .Entries<BaseEntity<Guid>>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var entity in entities)
        {
            var events = entity.DomainEvents.ToList();

            foreach (var domainEvent in events)
            {
                await _publisher.Publish(domainEvent, ct);
            }

            entity.ClearDomainEvents();
        }

        return result;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
