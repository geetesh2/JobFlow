using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JobFlow.IntegrationTests;

public class JobCreationTests : IClassFixture<JobFlowApiFactory>
{
    private readonly HttpClient _client;

    public JobCreationTests(JobFlowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateJob_Returns201_WithLocationHeader()
    {
        var payload = new { Name = "Test Job", Priority = "Normal", MaxRetries = 3 };

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out var idProp));
        Assert.NotEqual(Guid.Empty, idProp.GetGuid());
    }

    [Fact]
    public async Task CreateJob_Returns201_WithMinimalFields()
    {
        var payload = new { Name = "Minimal Job" };

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_Returns400_WhenNameIsEmpty()
    {
        var payload = new { Name = "", Priority = "Normal" };

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_Returns400_WhenNameExceedsMaxLength()
    {
        var payload = new { Name = new string('x', 201) };

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_Returns400_WhenPriorityIsInvalid()
    {
        var payload = new { Name = "Test Job", Priority = "NotALevel" };

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_Returns400_WhenMaxRetriesExceedsLimit()
    {
        var payload = new { Name = "Test Job", MaxRetries = 11 };

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
