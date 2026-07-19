using System.Text.Json;
using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Contracts.Messages;
using JobFlow.Domain.Enums;
using JobFlow.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace JobFlow.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConnection _rabbitConnection;
    private IChannel? _channel;

    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory serviceScopeFactory,
        IConnection rabbitConnection)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _rabbitConnection = rabbitConnection;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _rabbitConnection.CreateChannelAsync(new CreateChannelOptions(false, false, null, 0), stoppingToken);
        await _channel.ExchangeDeclareAsync("jobflow.exchange", ExchangeType.Direct, durable: true, autoDelete: false, arguments: null, passive: false, noWait: false, cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync("jobflow.job-created", durable: true, exclusive: false, autoDelete: false, arguments: null, passive: false, noWait: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync("jobflow.job-created", "jobflow.exchange", "job.created", arguments: null, noWait: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) => await HandleMessageAsync(ea, stoppingToken);

        await _channel.BasicConsumeAsync("jobflow.job-created", autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        _logger.LogInformation("Worker started and listening for job.created messages.");
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs message, CancellationToken cancellationToken)
    {
        var headers = message.BasicProperties.Headers;
        var traceId = headers != null && headers.TryGetValue("TraceId", out var t) ? t?.ToString() : null;
        using var activity = new System.Diagnostics.ActivitySource("JobFlow.Worker").StartActivity("ProcessJob", System.Diagnostics.ActivityKind.Consumer, traceId);

        try
        {
            var body = message.Body.ToArray();
            var jobMessage = JsonSerializer.Deserialize<JobCreatedMessage>(body);

            if (jobMessage is null)
            {
                _logger.LogWarning("Received invalid job created message payload.");
                if (_channel is not null)
                {
                    await _channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
                }
                return;
            }

                using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var statusUpdater = scope.ServiceProvider.GetRequiredService<WorkerJobStatusUpdater>();

            var job = await dbContext.Jobs.FindAsync(new object[] { jobMessage.JobId }, cancellationToken);

            if (job is null)
            {
                _logger.LogWarning("Job {JobId} not found while processing message.", jobMessage.JobId);
                if (_channel is not null)
                {
                    await _channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
                }
                return;
            }

            var jobDocument = await statusUpdater.GetJobAsync(jobMessage.JobId, cancellationToken);
            if (jobDocument is not null)
            {
                _logger.LogInformation("Loaded job payload for processing: {JobId} payload={Payload}", jobMessage.JobId, jobDocument.Payload);
            }
            else
            {
                _logger.LogInformation("No MongoDB job document found for {JobId}; processing SQL job record only.", jobMessage.JobId);
            }

            job.MarkAsProcessing();
            await dbContext.SaveChangesAsync(cancellationToken);
            await statusUpdater.UpdateStatusAsync(jobMessage.JobId, JobStatus.Processing, DateTime.UtcNow, cancellationToken);

            _logger.LogInformation("Processing job {JobId} ({Name})", job.Id, job.Name);
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            job.MarkAsCompleted();
            await dbContext.SaveChangesAsync(cancellationToken);
            await statusUpdater.UpdateStatusAsync(jobMessage.JobId, JobStatus.Completed, DateTime.UtcNow, cancellationToken);

            if (_channel is not null)
            {
                await _channel.BasicAckAsync(message.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
            }
            _logger.LogInformation("Job {JobId} completed.", job.Id);
        }
        catch (OperationCanceledException)
        {
            if (_channel is not null)
            {
                await _channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: true, cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process job message.");
            if (_channel is not null)
            {
                await _channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: true, cancellationToken: cancellationToken);
            }
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
    }
}
