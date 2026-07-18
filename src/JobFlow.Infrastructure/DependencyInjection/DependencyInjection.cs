using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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

        return services;
    }
}
