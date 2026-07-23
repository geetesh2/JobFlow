using JobFlow.Domain.Common.Events;
using JobFlow.Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly;

namespace JobFlow.Infrastructure.EventHandlers;

public sealed class JobStatusChangedEventHandler(
    WorkerJobStatusUpdater statusUpdater,
    ResiliencePipeline resiliencePipeline,
    ILogger<JobStatusChangedEventHandler> logger)
    : INotificationHandler<JobStatusChangedEvent>
{
    public async Task Handle(JobStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Job {JobId} status changed from {OldStatus} to {NewStatus}",
            notification.JobId, notification.OldStatus, notification.NewStatus);

        try
        {
            await resiliencePipeline.ExecuteAsync(
                async ct => await statusUpdater.UpdateStatusAsync(
                    notification.JobId, notification.NewStatus, DateTime.UtcNow, ct),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync status change for job {JobId} to MongoDB/Elasticsearch.", notification.JobId);
        }
    }
}
