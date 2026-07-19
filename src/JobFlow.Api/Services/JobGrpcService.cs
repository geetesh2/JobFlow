using Grpc.Core;
using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Contracts.Grpc;

namespace JobFlow.Api.Services;

public class JobGrpcServiceImpl : JobGrpcService.JobGrpcServiceBase
{
    private readonly IApplicationDbContext _dbContext;

    public JobGrpcServiceImpl(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task<JobStatusResponse> GetJobStatus(GetJobStatusRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.JobId, out var jobId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid Job ID"));
        }

        var job = await _dbContext.Jobs.FindAsync(new object[] { jobId }, context.CancellationToken);
        if (job == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Job {jobId} not found"));
        }

        return new JobStatusResponse
        {
            JobId = job.Id.ToString(),
            Status = job.Status.ToString(),
            UpdatedAtUtc = job.UpdatedAtUtc.ToString("O")
        };
    }
}
