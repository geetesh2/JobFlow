using JobFlow.Application.DTOs;
using JobFlow.Application.Interfaces;

namespace JobFlow.Api.GraphQL;

public class JobQuery
{
    public async Task<IEnumerable<JobResponse>> GetJobs([Service] IJobService jobService, CancellationToken ct)
    {
        return await jobService.GetAllJobsAsync(ct);
    }
}
