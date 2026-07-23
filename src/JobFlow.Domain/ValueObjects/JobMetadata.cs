namespace JobFlow.Domain.ValueObjects;

public sealed class JobMetadata
{
    public List<string> Tags { get; private set; } = [];
    public string? Source { get; private set; }
    public DateTime? ScheduledAtUtc { get; private set; }

    private JobMetadata() { }

    public JobMetadata(List<string>? tags = null, string? source = null, DateTime? scheduledAtUtc = null)
    {
        Tags = tags ?? [];
        Source = source;
        ScheduledAtUtc = scheduledAtUtc;
    }
}
