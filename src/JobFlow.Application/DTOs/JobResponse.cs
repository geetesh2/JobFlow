namespace JobFlow.Application.DTOs;

public sealed record JobResponse(
    Guid Id,
    string Name,
    string Status,
    string Priority,
    int RetryCount,
    int MaxRetries,
    int ProgressPercentage,
    string? ErrorMessage,
    string? CreatedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? CompletedAtUtc);
