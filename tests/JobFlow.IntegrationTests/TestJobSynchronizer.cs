using JobFlow.Application.Abstractions.Services;
using JobFlow.Domain.Entities;

namespace JobFlow.IntegrationTests;

public sealed class TestJobSynchronizer : IJobSynchronizer
{
    public Task SynchronizeAsync(Job job, IDictionary<string, object?>? metadata = null, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class TestJobIndexer : IJobIndexer
{
    public Task IndexAsync(Job job, string? payload, IDictionary<string, object?>? metadata = null, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
