using JobFlow.Api.Services;
using JobFlow.Infrastructure.DependencyInjection;
using JobFlow.Api.Authentication;
using JobFlow.Api.Endpoints;
using JobFlow.Api.Extensions;
using JobFlow.Api.Middleware;
using JobFlow.Application.Behaviors;
using JobFlow.Infrastructure.Persistence;
using JobFlow.Infrastructure.Services;
using FluentValidation;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddGrpc();
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

// Automatically skip external initializers when in Test environment
// Production and Development environments will connect by default
bool skipExternalInitializers = builder.Environment.EnvironmentName.Contains("Test", StringComparison.OrdinalIgnoreCase);

builder.Services.AddInfrastructure(builder.Configuration, skipExternalInitializers);
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(JobFlow.Application.DTOs.JobResponse).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(JobFlow.Application.DTOs.JobResponse).Assembly);
builder.Services.AddCors();
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddJobFlowRateLimiting(builder.Configuration, skipExternalInitializers);
builder.Services
    .AddGraphQLServer()
    .AddQueryType<JobFlow.Api.GraphQL.JobQuery>();

var app = builder.Build();

// Apply pending EF Core migrations (safe for Docker startup, no-op if already applied)
if (!skipExternalInitializers)
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }
}

if (!skipExternalInitializers)
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

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
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
app.MapGrpcService<JobGrpcServiceImpl>();
app.MapIdentityEndpoints();
app.MapJobEndpoints();

await app.RunAsync();

public partial class Program { }