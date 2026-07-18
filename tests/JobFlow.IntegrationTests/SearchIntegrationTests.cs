using System.Net.Http.Json;
using JobFlow.Application.DTOs;
using Xunit;

namespace JobFlow.IntegrationTests;

public class SearchIntegrationTests : IClassFixture<JobFlowApiFactory>
{
    private readonly HttpClient _client;

    public SearchIntegrationTests(JobFlowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SearchJobsAsync_ReturnsPagedResults()
    {
        var response = await _client.GetAsync("/api/v1/jobs?page=1&pageSize=5&query=test");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JobSearchResult>();
        Assert.NotNull(result);
        Assert.NotNull(result!.Jobs);
        Assert.Equal(1, result.Page);
        Assert.Equal(5, result.PageSize);
    }
}
