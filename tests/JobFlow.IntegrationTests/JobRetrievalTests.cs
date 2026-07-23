using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobFlow.Application.DTOs;

namespace JobFlow.IntegrationTests;

public class JobRetrievalTests : IClassFixture<JobFlowApiFactory>
{
    private readonly HttpClient _client;

    public JobRetrievalTests(JobFlowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetJob_Returns200_WithFullResponse()
    {
        var createPayload = new { Name = "Retrieval Test Job", Priority = "High", MaxRetries = 5 };
        var createResponse = await _client.PostAsJsonAsync("/api/v1/jobs", createPayload);
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = created.GetProperty("id").GetGuid();

        var getResponse = await _client.GetAsync($"/api/v1/jobs/{jobId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var job = await getResponse.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(job);
        Assert.Equal("Retrieval Test Job", job!.Name);
        Assert.Equal("Pending", job.Status);
        Assert.Equal("High", job.Priority);
        Assert.Equal(5, job.MaxRetries);
        Assert.Equal(0, job.RetryCount);
        Assert.Equal(0, job.ProgressPercentage);
    }

    [Fact]
    public async Task GetJob_Returns404_WhenJobDoesNotExist()
    {
        var response = await _client.GetAsync($"/api/v1/jobs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
