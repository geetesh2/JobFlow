using Grpc.Core;
using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Domain.Enums;
using JobFlow.Infrastructure.Services;
using JobFlow.Shared.Grpc;

namespace JobFlow.Worker.Services;

public class JobControlService : JobControl.JobControlBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly WorkerJobStatusUpdater _statusUpdater;
    private readonly ILogger<JobControlService> _logger;

    public JobControlService(
        IApplicationDbContext dbContext,
        WorkerJobStatusUpdater statusUpdater,
        ILogger<JobControlService> logger)
    {
        _dbContext = dbContext;
        _statusUpdater = statusUpdater;
        _logger = logger;
    }

    public override async Task<JobStatusResponse> UpdateJobStatus(JobStatusRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Updating job {JobId} status to {Status}", request.JobId, request.Status);

        if (!Guid.TryParse(request.JobId, out var jobId))
        {
            return new JobStatusResponse { Success = false, Message = "Invalid JobId format" };
        }

        if (!Enum.TryParse<JobStatus>(request.Status, true, out var status))
        {
            return new JobStatusResponse { Success = false, Message = "Invalid Status" };
        }

        var job = await _dbContext.Jobs.FindAsync(new object[] { jobId }, context.CancellationToken);
        if (job is null)
        {
            return new JobStatusResponse { Success = false, Message = "Job not found" };
        }

        // Logic to update status
        switch (status)
        {
            case JobStatus.Processing:
                job.MarkAsProcessing();
                break;
            case JobStatus.Completed:
                job.MarkAsCompleted();
                break;
            case JobStatus.Failed:
                job.MarkAsFailed();
                break;
            default:
                return new JobStatusResponse { Success = false, Message = "Unsupported status update" };
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);
        await _statusUpdater.UpdateStatusAsync(jobId, status, DateTime.UtcNow, context.CancellationToken);

        return new JobStatusResponse
        {
            Success = true,
            Message = "Status updated successfully"
        };
    }
}
