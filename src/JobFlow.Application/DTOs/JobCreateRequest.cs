using System.Text.Json;

namespace JobFlow.Application.DTOs;

public sealed record JobCreateRequest(
    string Name,
    JsonElement? Payload = null,
    Dictionary<string, JsonElement>? Metadata = null);
