using MediatR;

namespace JobFlow.Application.Commands.CreateJob;

public sealed record CreateJobCommand(
    string Name,
    string? Priority,
    string? Payload,
    int MaxRetries = 3,
    string? CreatedBy = null,
    List<string>? Tags = null,
    string? Source = null) : IRequest<Guid>;
