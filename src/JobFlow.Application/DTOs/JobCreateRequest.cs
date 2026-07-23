using System.Text.Json;

namespace JobFlow.Application.DTOs;

public sealed record JobCreateRequest(
    string Name,
    string? Priority = null,
    JsonElement? Payload = null,
    int MaxRetries = 3,
    Dictionary<string, JsonElement>? Metadata = null,
    List<string>? Tags = null,
    string? Source = null,
    DateTime? ScheduledAtUtc = null);
