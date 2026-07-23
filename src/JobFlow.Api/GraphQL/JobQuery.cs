using JobFlow.Application.DTOs;
using JobFlow.Application.Queries.GetJobById;
using MediatR;

namespace JobFlow.Api.GraphQL;

public class JobQuery
{
    public async Task<JobResponse?> GetJob([Service] ISender sender, Guid id, CancellationToken ct)
    {
        return await sender.Send(new GetJobByIdQuery(id), ct);
    }
}
