using JobFlow.Domain.Entities;
using JobFlow.Worker.Handlers;
using System.Text.Json;

namespace JobFlow.Worker.Handlers;

public sealed class ExternalApiJobHandler : IJobHandler
{
    private readonly ILogger<ExternalApiJobHandler> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ExternalApiJobHandler(ILogger<ExternalApiJobHandler> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public string JobType => "DogApi";

    public async Task HandleAsync(Job job, string? payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing Dog API Job: {JobId}", job.Id);

        var client = _httpClientFactory.CreateClient("DogApi");

        var response = await client.GetAsync("breeds/image/random", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Dog API call successful for job {JobId}. Response: {Content}", job.Id, content);
        }
        else
        {
            _logger.LogError("Dog API call failed for job {JobId}. Status: {StatusCode}", job.Id, response.StatusCode);
            throw new HttpRequestException($"Dog API call failed with status {response.StatusCode}", null, response.StatusCode);
        }
    }
}
