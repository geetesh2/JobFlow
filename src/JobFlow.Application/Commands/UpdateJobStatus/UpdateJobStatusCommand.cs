using MediatR;

namespace JobFlow.Application.Commands.UpdateJobStatus;

public sealed record UpdateJobStatusCommand(Guid JobId, string Status) : IRequest<bool>;
