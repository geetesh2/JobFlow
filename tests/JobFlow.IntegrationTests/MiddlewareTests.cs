using System.Net;

namespace JobFlow.IntegrationTests;

public class MiddlewareTests : IClassFixture<JobFlowApiFactory>
{
    private readonly HttpClient _client;

    public MiddlewareTests(JobFlowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CorrelationId_IsEchoed_WhenProvided()
    {
        var correlationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/jobs");
        request.Headers.Add("X-Correlation-ID", correlationId);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-ID").First());
    }

    [Fact]
    public async Task CorrelationId_IsGenerated_WhenNotProvided()
    {
        var response = await _client.GetAsync("/api/v1/jobs");

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        var generatedId = response.Headers.GetValues("X-Correlation-ID").First();
        Assert.False(string.IsNullOrWhiteSpace(generatedId));
    }
}
