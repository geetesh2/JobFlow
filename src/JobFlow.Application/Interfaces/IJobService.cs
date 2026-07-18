using JobFlow.Application.DTOs;

namespace JobFlow.Application.Interfaces;

public interface IJobService
{
    Task<JobResponse> CreateJobAsync(JobCreateRequest request, CancellationToken cancellationToken = default);

    Task<JobResponse?> GetJobAsync(Guid id, CancellationToken cancellationToken = default);
}
