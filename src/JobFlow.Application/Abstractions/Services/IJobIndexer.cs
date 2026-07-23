using JobFlow.Domain.Entities;

namespace JobFlow.Application.Abstractions.Services;

public interface IJobIndexer
{
    Task IndexAsync(Job job, string? payload, IDictionary<string, object?>? metadata = null, CancellationToken ct = default);
}
