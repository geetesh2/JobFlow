using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Application.Abstractions.Services;
using JobFlow.Application.Interfaces;
using JobFlow.Infrastructure.Persistence;
using JobFlow.Infrastructure.Persistence.Repositories;
using JobFlow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Nest;
using Npgsql;
using RabbitMQ.Client;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace JobFlow.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool skipExternalServices = false)
    {
        var defaultPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential
            })
            .Build();
        services.AddSingleton(defaultPipeline);

        services.AddKeyedSingleton("rabbitmq", BuildResiliencePipeline(timeoutSeconds: 10));
        services.AddKeyedSingleton("mongodb", BuildResiliencePipeline(timeoutSeconds: 15));
        services.AddKeyedSingleton("elasticsearch", BuildResiliencePipeline(timeoutSeconds: 15));

        var connectionString = configuration.GetConnectionString("JobFlowDb")
            ?? throw new InvalidOperationException("Connection string 'JobFlowDb' was not found.");

        var databasePassword = configuration["Database:Password"]
            ?? configuration["POSTGRES_PASSWORD"];

        if (!string.IsNullOrWhiteSpace(databasePassword))
        {
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Password = databasePassword
            };

            connectionString = connectionStringBuilder.ConnectionString;
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IJobRepository, EfJobRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        var redisConfig = configuration.GetValue<string>("Redis:Configuration")
            ?? throw new InvalidOperationException("Redis:Configuration is not configured.");
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConfig;
            options.InstanceName = configuration.GetValue("Redis:InstanceName", "JobFlow:");
        });

        var mongoConnectionString = configuration.GetValue<string>("MongoDb:ConnectionString")
            ?? throw new InvalidOperationException("MongoDb:ConnectionString is not configured.");
        services.AddSingleton<IMongoClient>(provider =>
        {
            MongoDbConfig.Configure();
            return new MongoClient(mongoConnectionString);
        });

        var elasticsearchUrl = configuration.GetValue<string>("Elasticsearch:Url")
            ?? throw new InvalidOperationException("Elasticsearch:Url is not configured.");
        services.AddSingleton<IElasticClient>(provider =>
        {
            var settings = new ConnectionSettings(new Uri(elasticsearchUrl))
                .DefaultIndex(configuration.GetValue("Elasticsearch:DefaultIndex", "jobflow-jobs"));

            return new ElasticClient(settings);
        });

        services.AddScoped<IJobSearchService, ElasticJobSearchService>();
        services.AddScoped<MongoJobSynchronizer>();
        services.AddScoped<IJobSynchronizer>(sp => sp.GetRequiredService<MongoJobSynchronizer>());
        services.AddSingleton<MongoDbIndexInitializer>();
        services.AddSingleton<ElasticsearchIndexInitializer>();
        services.AddScoped<ElasticJobIndexer>();
        services.AddScoped<IJobIndexer>(sp => sp.GetRequiredService<ElasticJobIndexer>());
        services.AddScoped<WorkerJobStatusUpdater>();

        var rabbitMqConfig = configuration.GetSection("RabbitMq");
        var rabbitConnectionFactory = new ConnectionFactory
        {
            HostName = rabbitMqConfig.GetValue<string>("Host")
                ?? throw new InvalidOperationException("RabbitMq:Host is not configured."),
            UserName = rabbitMqConfig.GetValue<string>("Username")
                ?? throw new InvalidOperationException("RabbitMq:Username is not configured."),
            Password = rabbitMqConfig.GetValue<string>("Password")
                ?? throw new InvalidOperationException("RabbitMq:Password is not configured.")
        };

        services.AddSingleton<IConnectionFactory>(_ => rabbitConnectionFactory);
        services.AddSingleton<RabbitMqConnectionInitializer>();
        
        if (!skipExternalServices)
        {
            services.AddHostedService(sp => sp.GetRequiredService<RabbitMqConnectionInitializer>());
        }

        // Register IConnection lazily through RabbitMqConnectionInitializer to avoid blocking startup.
        // The hosted service's StartAsync will eagerly connect; the initializer's GetConnectionAsync()
        // will lazily connect on first use for scenarios where StartAsync is skipped.
        services.AddSingleton<IConnection>(sp =>
        {
            var initializer = sp.GetRequiredService<RabbitMqConnectionInitializer>();
            return initializer.Connection;
        });
        services.AddScoped<IJobPublisher, RabbitMqJobPublisher>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<CachedJobQueryService>();
        services.AddScoped<IIdempotencyService, RedisIdempotencyService>();

        services.AddHealthChecks()
            .AddNpgSql(connectionString)
            .AddRedis(redisConfig)
            .AddRabbitMQ(sp => sp.GetRequiredService<IConnection>())
            .AddMongoDb(sp => sp.GetRequiredService<IMongoClient>())
            .AddElasticsearch(elasticsearchUrl);

        var otlpEndpoint = configuration.GetValue<string>("Otlp:Endpoint")
            ?? throw new InvalidOperationException("Otlp:Endpoint is not configured.");
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddEntityFrameworkCoreInstrumentation()
                       .AddSource("JobFlow.Worker")
                       .AddOtlpExporter(options =>
                       {
                           options.Endpoint = new Uri(otlpEndpoint);
                       });
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddPrometheusExporter();
            });

        return services;
    }

    private static ResiliencePipeline BuildResiliencePipeline(int timeoutSeconds) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .AddTimeout(TimeSpan.FromSeconds(timeoutSeconds))
            .Build();
}
