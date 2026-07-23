using JobFlow.Domain.Common;
using JobFlow.Domain.Common.Events;
using JobFlow.Domain.Enums;
using JobFlow.Domain.ValueObjects;

namespace JobFlow.Domain.Entities;

public sealed class Job : BaseEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public JobStatus Status { get; private set; }
    public JobPriority Priority { get; private set; }
    public string? Payload { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? CreatedBy { get; private set; }
    public int ProgressPercentage { get; private set; }
    public JobMetadata Metadata { get; private set; } = new();

    private Job() { }

    public Job(string name, JobPriority priority = JobPriority.Normal, string? payload = null,
        int maxRetries = 3, string? createdBy = null, JobMetadata? metadata = null)
    {
        Id = Guid.NewGuid();
        Name = name;
        Status = JobStatus.Pending;
        Priority = priority;
        Payload = payload;
        MaxRetries = maxRetries;
        CreatedBy = createdBy;
        Metadata = metadata ?? new();

        AddDomainEvent(new JobCreatedEvent(Id, Name, Priority.ToString()));
    }

    public void MarkAsProcessing()
    {
        if (Status is not JobStatus.Pending and not JobStatus.Failed and not JobStatus.Processing)
            throw new InvalidOperationException($"Cannot transition from {Status} to {JobStatus.Processing}.");

        var oldStatus = Status;
        Status = JobStatus.Processing;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new JobStatusChangedEvent(Id, oldStatus, JobStatus.Processing));
    }

    public void MarkAsCompleted()
    {
        if (Status is not JobStatus.Processing)
            throw new InvalidOperationException($"Cannot transition from {Status} to {JobStatus.Completed}.");

        Status = JobStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        ProgressPercentage = 100;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new JobCompletedEvent(Id, CompletedAtUtc!.Value));
    }

    public void MarkAsFailed(string? errorMessage = null)
    {
        if (Status is not JobStatus.Processing and not JobStatus.Pending)
            throw new InvalidOperationException($"Cannot transition from {Status} to {JobStatus.Failed}.");

        Status = JobStatus.Failed;
        ErrorMessage = errorMessage;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new JobFailedEvent(Id, errorMessage));
    }

    public void IncrementRetry()
    {
        RetryCount++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool CanRetry() => RetryCount < MaxRetries;

    public void UpdateProgress(int percentage)
    {
        ProgressPercentage = Math.Clamp(percentage, 0, 100);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetError(string errorMessage)
    {
        ErrorMessage = errorMessage;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
