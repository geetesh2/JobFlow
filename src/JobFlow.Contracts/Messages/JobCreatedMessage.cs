namespace JobFlow.Contracts.Messages;

public sealed record JobCreatedMessage(
    Guid JobId,
    string Name,
    DateTime CreatedAtUtc,
    string CorrelationId,
    string? TraceId = null);
