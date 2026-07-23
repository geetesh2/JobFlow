using JobFlow.Application.DTOs;
using MediatR;

namespace JobFlow.Application.Queries.SearchJobs;

public sealed record SearchJobsQuery(
    string? Query,
    string? Status,
    DateTime? CreatedAfterUtc,
    DateTime? CreatedBeforeUtc,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "CreatedAtUtc",
    string SortOrder = "desc") : IRequest<JobSearchResult>;
