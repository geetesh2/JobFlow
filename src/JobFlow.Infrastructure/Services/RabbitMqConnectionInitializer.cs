using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace JobFlow.Infrastructure.Services;

public sealed class RabbitMqConnectionInitializer : IHostedService
{
    private readonly IConnectionFactory _factory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqConnectionInitializer> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnectionInitializer(
        IConnectionFactory factory,
        IConfiguration configuration,
        ILogger<RabbitMqConnectionInitializer> logger)
    {
        _factory = factory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection != null && _connection.IsOpen)
        {
            return _connection;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection == null || !_connection.IsOpen)
            {
                _connection = await _factory.CreateConnectionAsync(cancellationToken);
            }
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public IConnection Connection
    {
        get
        {
            if (_connection != null && _connection.IsOpen)
                return _connection;

            // Blocking fallback for lazy resolution when StartAsync was skipped (e.g., local dev with SkipExternalInitializers)
            return GetConnectionAsync().GetAwaiter().GetResult();
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_configuration.GetValue("Test:SkipExternalInitializers", false))
        {
            _logger.LogInformation("Skipping RabbitMQ connection initialization (Test:SkipExternalInitializers=true).");
            return;
        }

        _connection = await _factory.CreateConnectionAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
