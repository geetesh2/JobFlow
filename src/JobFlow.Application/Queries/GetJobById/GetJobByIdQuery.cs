using JobFlow.Application.DTOs;
using MediatR;

namespace JobFlow.Application.Queries.GetJobById;

public sealed record GetJobByIdQuery(Guid Id) : IRequest<JobResponse?>;
