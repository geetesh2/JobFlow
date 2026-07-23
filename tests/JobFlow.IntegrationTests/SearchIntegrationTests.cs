using System.Net.Http.Json;
using System.Text.Json;
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

    [Fact]
    public async Task CreateJobAndSearch_ReturnsCreatedJobFromElasticsearch()
    {
        var createPayload = new
        {
            Name = "Integration Test Job",
            Payload = new { message = "hello search" },
            Metadata = new Dictionary<string, object?>
            {
                ["source"] = "integration-test"
            }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/jobs", createPayload);
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(created.TryGetProperty("id", out var idProp));
        var jobId = idProp.GetGuid();

        var getResponse = await _client.GetAsync($"/api/v1/jobs/{jobId}");
        getResponse.EnsureSuccessStatusCode();

        var job = await getResponse.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(job);
        Assert.Equal("Integration Test Job", job!.Name);
    }
}
