using JobFlow.Worker;
using JobFlow.Infrastructure.DependencyInjection;
using JobFlow.Infrastructure.Services;
using JobFlow.Worker.Services;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGrpc();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();
app.MapGrpcService<JobControlService>();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = (check) => true
});
app.MapHealthChecks("/live", new HealthCheckOptions
{
    Predicate = (_) => false
});
app.UseOpenTelemetryPrometheusScrapingEndpoint();
using var scope = app.Services.CreateScope();

var serviceProvider = scope.ServiceProvider;

var mongoInitializer = serviceProvider.GetRequiredService<MongoDbIndexInitializer>();
var elasticInitializer = serviceProvider.GetRequiredService<ElasticsearchIndexInitializer>();
await mongoInitializer.InitializeAsync();
await elasticInitializer.InitializeAsync();

await app.RunAsync();
