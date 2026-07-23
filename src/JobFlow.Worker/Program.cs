using JobFlow.Worker;
using JobFlow.Worker.Cancellation;
using JobFlow.Worker.Configuration;
using JobFlow.Worker.Execution;
using JobFlow.Worker.Handlers;
using JobFlow.Worker.Messaging;
using JobFlow.Worker.Progress;
using JobFlow.Worker.Retry;
using JobFlow.Worker.Services;
using JobFlow.Infrastructure.DependencyInjection;
using JobFlow.Infrastructure.Services;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using QuestPDF.Infrastructure;
using Serilog;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));
builder.Services.AddHttpClient("DogApi", client =>
{
    client.BaseAddress = new Uri("https://dog.ceo/api/");
});
builder.Services.AddHttpClient("ImageDownload");
// Automatically skip external initializers when in Test environment
// Production and Development environments will connect by default
bool skipExternalInitializers = builder.Environment.EnvironmentName.Contains("Test", StringComparison.OrdinalIgnoreCase);

builder.Services.AddInfrastructure(builder.Configuration, skipExternalInitializers);

// Worker configuration
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));

// Job handlers
builder.Services.AddScoped<IJobHandler, ExternalApiJobHandler>();
builder.Services.AddScoped<IJobHandler, DataProcessingJobHandler>();
builder.Services.AddScoped<IJobHandler, EmailJobHandler>();
builder.Services.AddScoped<IJobHandler, ImageResizeJobHandler>();
builder.Services.AddScoped<IJobHandler, PdfJobHandler>();

// Worker services
builder.Services.AddSingleton<IJobRetryPolicy, ExponentialRetryPolicy>();
builder.Services.AddSingleton<IJobCancellationService, JobCancellationService>();
builder.Services.AddScoped<IProgressReporter, ProgressReporter>();
builder.Services.AddScoped<IJobExecutor, JobExecutor>();
builder.Services.AddScoped<DeadLetterPublisher>();

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

if (!skipExternalInitializers)
{
    using (var scope = app.Services.CreateScope())
    {
        var mongoInitializer = scope.ServiceProvider.GetRequiredService<MongoDbIndexInitializer>();
        var elasticInitializer = scope.ServiceProvider.GetRequiredService<ElasticsearchIndexInitializer>();
        await mongoInitializer.InitializeAsync();
        await elasticInitializer.InitializeAsync();
    }
}

await app.RunAsync();
