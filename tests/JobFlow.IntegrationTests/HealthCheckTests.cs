using System.Net;

namespace JobFlow.IntegrationTests;

public class HealthCheckTests : IClassFixture<JobFlowApiFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(JobFlowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LiveEndpoint_Returns200()
    {
        var response = await _client.GetAsync("/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
