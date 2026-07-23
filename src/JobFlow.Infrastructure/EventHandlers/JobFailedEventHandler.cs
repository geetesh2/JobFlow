using JobFlow.Domain.Common.Events;
using JobFlow.Domain.Enums;
using JobFlow.Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly;

namespace JobFlow.Infrastructure.EventHandlers;

public sealed class JobFailedEventHandler(
    WorkerJobStatusUpdater statusUpdater,
    ResiliencePipeline resiliencePipeline,
    ILogger<JobFailedEventHandler> logger)
    : INotificationHandler<JobFailedEvent>
{
    public async Task Handle(JobFailedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Job {JobId} failed.", notification.JobId);

        try
        {
            await resiliencePipeline.ExecuteAsync(
                async ct => await statusUpdater.UpdateStatusAsync(
                    notification.JobId, JobStatus.Failed, DateTime.UtcNow, ct),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync failure for job {JobId} to MongoDB/Elasticsearch.", notification.JobId);
        }
    }
}