using JobFlow.Domain.Common.Events;
using JobFlow.Domain.Enums;
using JobFlow.Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly;

namespace JobFlow.Infrastructure.EventHandlers;

public sealed class JobCompletedEventHandler(
    WorkerJobStatusUpdater statusUpdater,
    ResiliencePipeline resiliencePipeline,
    ILogger<JobCompletedEventHandler> logger)
    : INotificationHandler<JobCompletedEvent>
{
    public async Task Handle(JobCompletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Job {JobId} completed.", notification.JobId);

        try
        {
            await resiliencePipeline.ExecuteAsync(
                async ct => await statusUpdater.UpdateStatusAsync(
                    notification.JobId, JobStatus.Completed, DateTime.UtcNow, ct),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync completion for job {JobId} to MongoDB/Elasticsearch.", notification.JobId);
        }
    }
}