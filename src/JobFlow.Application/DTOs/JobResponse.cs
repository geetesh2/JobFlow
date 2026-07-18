namespace JobFlow.Application.DTOs;

public sealed record JobResponse(
    Guid Id,
    string Name,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
