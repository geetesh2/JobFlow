using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Application.Abstractions.Services;
using JobFlow.Domain.Common.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly;

namespace JobFlow.Infrastructure.EventHandlers;

public sealed class JobCreatedEventHandler(
    IUnitOfWork unitOfWork,
    IJobSynchronizer jobSynchronizer,
    IJobIndexer jobIndexer,
    ResiliencePipeline resiliencePipeline,
    ILogger<JobCreatedEventHandler> logger)
    : INotificationHandler<JobCreatedEvent>
{
    public async Task Handle(JobCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain event: Job created {JobId}, Name: {Name}, Priority: {Priority}",
            notification.JobId, notification.Name, notification.Priority);

        var job = await unitOfWork.Jobs.GetByIdAsync(notification.JobId, cancellationToken);
        if (job is null) return;

        try
        {
            await resiliencePipeline.ExecuteAsync(
                async ct => await jobSynchronizer.SynchronizeAsync(job, null, ct), cancellationToken);
            await resiliencePipeline.ExecuteAsync(
                async ct => await jobIndexer.IndexAsync(job, job.Payload, null, ct), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync job {JobId} to MongoDB/Elasticsearch.", notification.JobId);
        }
    }
}
