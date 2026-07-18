namespace JobFlow.Application.DTOs;

public sealed record JobSearchRequest(
    string? Query,
    string? Status,
    DateTime? CreatedAfterUtc,
    DateTime? CreatedBeforeUtc,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "CreatedAtUtc",
    string SortOrder = "desc");
