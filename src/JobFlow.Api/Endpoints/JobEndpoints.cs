using JobFlow.Application.DTOs;
using JobFlow.Application.Interfaces;
using JobFlow.Api.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JobFlow.Api.Endpoints;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var jobs = endpoints.MapGroup("/api/v1/jobs");

        jobs.MapPost("", async (JobCreateRequest request, IJobService jobService) =>
        {
            var result = await jobService.CreateJobAsync(request);
            return Results.Created($"/api/v1/jobs/{result.Id}", result);
        })
        .RequireAuthorization(JobFlowPolicies.UserAccess)
        .WithName("CreateJob");

        jobs.MapGet("/{id:guid}", async (Guid id, IJobService jobService) =>
        {
            var result = await jobService.GetJobAsync(id);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization(JobFlowPolicies.UserAccess)
        .WithName("GetJob");

        jobs.MapGet("", async (IJobSearchService searchService, string? query, string? status, DateTime? createdAfterUtc, DateTime? createdBeforeUtc, int page = 1, int pageSize = 20, string sortBy = "CreatedAtUtc", string sortOrder = "desc") =>
        {
            var request = new JobSearchRequest(query, status, createdAfterUtc, createdBeforeUtc, page, pageSize, sortBy, sortOrder);
            var result = await searchService.SearchJobsAsync(request);
            return Results.Ok(result);
        })
        .RequireAuthorization(JobFlowPolicies.UserAccess)
        .WithName("SearchJobs");

        return endpoints;
    }
}
