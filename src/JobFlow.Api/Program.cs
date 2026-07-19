using JobFlow.Infrastructure.DependencyInjection;
using JobFlow.Api.Authentication;
using JobFlow.Api.Endpoints;
using JobFlow.Api.Extensions;
using JobFlow.Api.Middleware;
using JobFlow.Infrastructure.Services;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JobFlow API",
        Version = "v1"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a Keycloak access token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        }] = []
    });
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddJobFlowRateLimiting(builder.Configuration);
builder.Services
    .AddGraphQLServer()
    .AddQueryType<JobFlow.Api.GraphQL.JobQuery>();

var app = builder.Build();

if (!app.Configuration.GetValue("Test:SkipExternalInitializers", false))
{
    using (var scope = app.Services.CreateScope())
    {
        var serviceProvider = scope.ServiceProvider;
        var mongoInitializer = serviceProvider.GetRequiredService<MongoDbIndexInitializer>();
        var elasticInitializer = serviceProvider.GetRequiredService<ElasticsearchIndexInitializer>();

        await mongoInitializer.InitializeAsync();
        await elasticInitializer.InitializeAsync();
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "JobFlow API v1");
});

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseMiddleware<IdempotencyMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapGraphQL();

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
    Predicate = (_) => false // Only basic check if service is up, no dependencies
});
app.MapIdentityEndpoints();
app.MapJobEndpoints();

await app.RunAsync();

public partial class Program { }
