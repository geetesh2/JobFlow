using JobFlow.Domain.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace JobFlow.Infrastructure.Models;

public sealed class JobDocument
{
    [BsonId]
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required JobStatus Status { get; init; }
    public required JobPriority Priority { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
    public string? Payload { get; init; }
    public int RetryCount { get; init; }
    public int MaxRetries { get; init; }
    public int ProgressPercentage { get; init; }
    public string? ErrorMessage { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public IDictionary<string, object?>? Metadata { get; init; }
}
