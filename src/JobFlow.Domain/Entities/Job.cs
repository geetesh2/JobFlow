using JobFlow.Domain.Common;
using JobFlow.Domain.Enums;

namespace JobFlow.Domain.Entities;

public sealed class Job : BaseEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;

    public JobStatus Status { get; private set; }

    private Job()
    {
    }

    public Job(string name)
    {
        Id = Guid.NewGuid();
        Name = name;
        Status = JobStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsProcessing()
    {
        Status = JobStatus.Processing;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsCompleted()
    {
        Status = JobStatus.Completed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsFailed()
    {
        Status = JobStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
