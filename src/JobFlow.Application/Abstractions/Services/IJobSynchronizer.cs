using JobFlow.Domain.Entities;

namespace JobFlow.Application.Abstractions.Services;

public interface IJobSynchronizer
{
    Task SynchronizeAsync(Job job, IDictionary<string, object?>? metadata = null, CancellationToken ct = default);
}
