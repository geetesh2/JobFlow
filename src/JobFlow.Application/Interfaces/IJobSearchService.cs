using JobFlow.Application.DTOs;

namespace JobFlow.Application.Interfaces;

public interface IJobSearchService
{
    Task<JobSearchResult> SearchJobsAsync(JobSearchRequest request, CancellationToken cancellationToken = default);
}
