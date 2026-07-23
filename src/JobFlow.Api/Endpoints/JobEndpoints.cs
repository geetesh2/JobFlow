using JobFlow.Application.Commands.CreateJob;
using JobFlow.Application.DTOs;
using JobFlow.Application.Queries.GetJobById;
using JobFlow.Application.Queries.SearchJobs;
using JobFlow.Api.Authentication;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace JobFlow.Api.Endpoints;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var jobs = endpoints.MapGroup("/api/v1/jobs");

        jobs.MapPost("", async (JobCreateRequest request, ISender sender) =>
        {
            var command = new CreateJobCommand(
                request.Name,
                request.Priority,
                request.Payload.HasValue ? request.Payload.Value.GetRawText() : null,
                request.MaxRetries,
                null,
                request.Tags,
                request.Source);

            var jobId = await sender.Send(command);
            return Results.Created($"/api/v1/jobs/{jobId}", new { id = jobId });
        })
        .RequireAuthorization(JobFlowPolicies.UserAccess)
        .RequireRateLimiting("job-submission")
        .WithName("CreateJob");

        jobs.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetJobByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization(JobFlowPolicies.UserAccess)
        .RequireRateLimiting("global")
        .WithName("GetJob");

        jobs.MapGet("", async (ISender sender, string? query, string? status, DateTime? createdAfterUtc, DateTime? createdBeforeUtc, int page = 1, int pageSize = 20, string sortBy = "CreatedAtUtc", string sortOrder = "desc") =>
        {
            var result = await sender.Send(new SearchJobsQuery(query, status, createdAfterUtc, createdBeforeUtc, page, pageSize, sortBy, sortOrder));
            return Results.Ok(result);
        })
        .RequireAuthorization(JobFlowPolicies.UserAccess)
        .RequireRateLimiting("global")
        .WithName("SearchJobs");

        return endpoints;
    }
}
