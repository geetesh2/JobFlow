namespace JobFlow.Contracts.Messages;

public sealed record JobCreatedMessage(
    Guid JobId,
    string Name,
    string Priority,
    string? Payload,
    int MaxRetries,
    DateTime CreatedAtUtc,
    string CorrelationId,
    string? TraceId = null);
