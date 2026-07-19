using JobFlow.Application.Interfaces;
using JobFlow.Application.DTOs;

namespace JobFlow.IntegrationTests;

public interface ITestJobStore
{
    IReadOnlyCollection<JobResponse> GetAll();
}

public sealed class TestJobService : IJobService, ITestJobStore
{
    private static readonly Dictionary<Guid, JobResponse> _store = new();

    public Task<JobResponse> CreateJobAsync(JobCreateRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var resp = new JobResponse(Guid.NewGuid(), request.Name, "Pending", now, now);
        _store[resp.Id] = resp;
        return Task.FromResult(resp);
    }

    public Task<JobResponse?> GetJobAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var resp);
        return Task.FromResult(resp);
    }

    public Task<IEnumerable<JobResponse>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((IEnumerable<JobResponse>)_store.Values.ToList());
    }

    public IReadOnlyCollection<JobResponse> GetAll() => _store.Values.ToList().AsReadOnly();
}
