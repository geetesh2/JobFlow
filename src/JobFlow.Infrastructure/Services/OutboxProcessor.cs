using System.Text.Json;
using JobFlow.Application.Interfaces;
using JobFlow.Contracts.Messages;
using JobFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobFlow.Infrastructure.Services;

public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessOutboxMessagesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IJobPublisher>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        var messages = await dbContext.OutboxMessages
            .FromSqlRaw("""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAtUtc" IS NULL AND "RetryCount" < 5
                ORDER BY "CreatedAtUtc"
                LIMIT 20
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                switch (message.Type)
                {
                    case nameof(JobCreatedMessage):
                        var jobMessage = JsonSerializer.Deserialize<JobCreatedMessage>(message.Payload);
                        if (jobMessage is null)
                        {
                            message.MarkAsFailed("Failed to deserialize payload.");
                            break;
                        }
                        await publisher.PublishJobCreatedAsync(jobMessage, ct);
                        message.MarkAsProcessed();
                        break;

                    default:
                        _logger.LogWarning("Unknown outbox message type '{Type}' for message {Id}. Skipping.", message.Type, message.Id);
                        break;
                }

                if (message.ProcessedAtUtc is not null)
                    _logger.LogInformation("Outbox message {Id} published.", message.Id);
            }
            catch (Exception ex)
            {
                message.MarkAsFailed(ex.Message);
                _logger.LogWarning(ex, "Failed to publish outbox message {Id}.", message.Id);
            }
        }

        if (messages.Count > 0)
            await dbContext.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
