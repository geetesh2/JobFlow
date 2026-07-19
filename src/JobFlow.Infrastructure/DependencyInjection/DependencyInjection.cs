using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Application.Abstractions.Services;
using JobFlow.Application.Interfaces;
using JobFlow.Infrastructure.Persistence;
using JobFlow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Nest;
using Npgsql;
using RabbitMQ.Client;
using Polly;
using Polly.Retry;

namespace JobFlow.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Constant
            })
            .Build();

        services.AddSingleton(resiliencePipeline);
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

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetValue("Redis:Configuration", "localhost:6379");
            options.InstanceName = configuration.GetValue("Redis:InstanceName", "JobFlow:");
        });

        services.AddSingleton<IMongoClient>(provider =>
        {
            var connectionString = configuration.GetValue("MongoDb:ConnectionString", "mongodb://localhost:27017");
            return new MongoClient(connectionString);
        });

        services.AddSingleton<IElasticClient>(provider =>
        {
            var url = configuration.GetValue("Elasticsearch:Url", "http://localhost:9200");
            var settings = new ConnectionSettings(new Uri(url))
                .DefaultIndex(configuration.GetValue("Elasticsearch:DefaultIndex", "jobflow-jobs"));

            return new ElasticClient(settings);
        });

        services.AddScoped<IJobSearchService, ElasticJobSearchService>();
        services.AddScoped<MongoJobSynchronizer>();
        services.AddScoped<MongoDbIndexInitializer>();
        services.AddScoped<ElasticsearchIndexInitializer>();
        services.AddScoped<ElasticJobIndexer>();
        services.AddScoped<WorkerJobStatusUpdater>();

        var rabbitMqConfig = configuration.GetSection("RabbitMq");
        var rabbitConnectionFactory = new ConnectionFactory
        {
            HostName = rabbitMqConfig.GetValue("Host", "localhost"),
            UserName = rabbitMqConfig.GetValue("Username", "guest"),
            Password = rabbitMqConfig.GetValue("Password", "guest")
        };

        services.AddSingleton<IConnection>(_ => rabbitConnectionFactory.CreateConnectionAsync().GetAwaiter().GetResult());
        services.AddScoped<IJobPublisher, RabbitMqJobPublisher>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IIdempotencyService, RedisIdempotencyService>();

        return services;
    }
}
