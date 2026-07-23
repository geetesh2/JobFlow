using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Domain.Entities;
using JobFlow.Domain.Enums;

namespace JobFlow.IntegrationTests;

public sealed class TestJobRepository : IJobRepository
{
    private readonly Dictionary<Guid, Job> _store = new();

    public Task AddAsync(Job entity, CancellationToken ct = default)
    {
        _store[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<Job>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Job>>(_store.Values.ToList().AsReadOnly());
    }

    public Task<IReadOnlyList<Job>> GetByStatusAsync(JobStatus status, CancellationToken ct = default)
    {
        var result = _store.Values.Where(j => j.Status == status).ToList().AsReadOnly();
        return Task.FromResult<IReadOnlyList<Job>>(result);
    }

    public Task<IReadOnlyList<Job>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var result = _store.Values
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<Job>>(result);
    }

    public void Update(Job entity)
    {
        _store[entity.Id] = entity;
    }

    public void Delete(Job entity)
    {
        _store.Remove(entity.Id);
    }
}
