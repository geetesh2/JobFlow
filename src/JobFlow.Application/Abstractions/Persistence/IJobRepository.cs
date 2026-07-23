using JobFlow.Domain.Entities;
using JobFlow.Domain.Enums;

namespace JobFlow.Application.Abstractions.Persistence;

public interface IJobRepository : IRepository<Job>
{
    Task<IReadOnlyList<Job>> GetByStatusAsync(JobStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Job>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
}
