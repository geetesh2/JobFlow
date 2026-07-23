namespace JobFlow.Application.DTOs;

public sealed record JobSearchResult(
    IEnumerable<JobResponse> Jobs,
    int Page,
    int PageSize,
    long Total);
