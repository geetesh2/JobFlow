using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Application.Interfaces;
using JobFlow.Infrastructure.Persistence;
using JobFlow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RabbitMQ.Client;

namespace JobFlow.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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

        return services;
    }
}
