namespace JobFlow.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(string type, string payload)
    {
        Id = Guid.NewGuid();
        Type = type;
        Payload = payload;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsProcessed() { ProcessedAtUtc = DateTime.UtcNow; }
    public void MarkAsFailed(string error) { Error = error; RetryCount++; }
}
