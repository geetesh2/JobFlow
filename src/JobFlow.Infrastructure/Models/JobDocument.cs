using JobFlow.Domain.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace JobFlow.Infrastructure.Models;

public sealed class JobDocument
{
    [BsonId]
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required JobStatus Status { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
    public required string Payload { get; init; }
    public required IDictionary<string, object?> Metadata { get; init; }
}
