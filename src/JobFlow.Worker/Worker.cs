using System.Text.Json;
using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Contracts.Messages;
using JobFlow.Worker.Cancellation;
using JobFlow.Worker.Execution;
using JobFlow.Worker.Messaging;
using JobFlow.Worker.Retry;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace JobFlow.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConnection _rabbitConnection;
    private readonly IJobCancellationService _cancellationService;
    private IChannel? _channel;

    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory serviceScopeFactory,
        IConnection rabbitConnection,
        IJobCancellationService cancellationService)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _rabbitConnection = rabbitConnection;
        _cancellationService = cancellationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _channel = await _rabbitConnection.CreateChannelAsync(cancellationToken: stoppingToken);
            await _channel.ExchangeDeclareAsync("jobflow.exchange", ExchangeType.Direct, durable: true, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync("jobflow.job-created", durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
            await _channel.QueueBindAsync("jobflow.job-created", "jobflow.exchange", "job.created", cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                _logger.LogInformation("Message arrived in handler.");
                await HandleMessageAsync(ea, _channel, stoppingToken);
            };

            await _channel.BasicConsumeAsync("jobflow.job-created", autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
            _logger.LogInformation("Worker started and listening for job.created messages.");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker service is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RabbitMQ consumer background service.");
        }
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs message, IChannel channel, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Entering HandleMessageAsync.");
        try
        {
            var body = message.Body.ToArray();
            var jobMessage = JsonSerializer.Deserialize<JobCreatedMessage>(body);

            if (jobMessage is null)
            {
                _logger.LogWarning("Received invalid job created message payload.");
                return;
            }

            _logger.LogInformation("Processing job: {JobId}", jobMessage.JobId);

            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var jobExecutor = scope.ServiceProvider.GetRequiredService<IJobExecutor>();
            var retryPolicy = scope.ServiceProvider.GetRequiredService<IJobRetryPolicy>();
            var deadLetterPublisher = scope.ServiceProvider.GetRequiredService<DeadLetterPublisher>();

            var job = await unitOfWork.Jobs.GetByIdAsync(jobMessage.JobId, cancellationToken);

            if (job is null)
            {
                _logger.LogWarning("Job {JobId} not found while processing message.", jobMessage.JobId);
                await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
                return;
            }

            var jobCancellationToken = _cancellationService.Register(job.Id);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, jobCancellationToken);

            try
            {
                job.MarkAsProcessing();
                // UoW.SaveChangesAsync dispatches domain events -> JobStatusChangedEventHandler syncs to Mongo/ES
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var result = await jobExecutor.ExecuteAsync(job, job.Payload, linkedCts.Token);

                if (result.Success)
                {
                    job.MarkAsCompleted();
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await channel.BasicAckAsync(message.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
                    _logger.LogInformation("Job {JobId} completed in {Duration}.", job.Id, result.Duration);
                }
                else
                {
                    await HandleFailureWithRetryAsync(job, result.ErrorMessage, body, message, channel,
                        unitOfWork, jobExecutor, retryPolicy, deadLetterPublisher, linkedCts.Token, cancellationToken);
                }
            }
            finally
            {
                _cancellationService.Unregister(job.Id);
            }
        }
        catch (OperationCanceledException)
        {
            await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: true, cancellationToken: CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process job message.");
            await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: true, cancellationToken: CancellationToken.None);
        }
    }

    private async Task HandleFailureWithRetryAsync(
        Domain.Entities.Job job,
        string? errorMessage,
        byte[] originalBody,
        BasicDeliverEventArgs message,
        IChannel channel,
        IUnitOfWork unitOfWork,
        IJobExecutor jobExecutor,
        IJobRetryPolicy retryPolicy,
        DeadLetterPublisher deadLetterPublisher,
        CancellationToken jobToken,
        CancellationToken stoppingToken)
    {
        _logger.LogWarning("Job {JobId} failed: {Error}. Checking retry policy.", job.Id, errorMessage);

        while (job.CanRetry() && retryPolicy.ShouldRetry(job.RetryCount, new Exception(errorMessage ?? "Unknown error")))
        {
            job.IncrementRetry();
            var delay = retryPolicy.GetDelay(job.RetryCount);
            _logger.LogInformation("Retrying job {JobId} (attempt {Attempt}) after {Delay}.", job.Id, job.RetryCount, delay);

            job.MarkAsProcessing();
            await unitOfWork.SaveChangesAsync(stoppingToken);

            await Task.Delay(delay, stoppingToken);

            var retryResult = await jobExecutor.ExecuteAsync(job, job.Payload, jobToken);
            if (retryResult.Success)
            {
                job.MarkAsCompleted();
                await unitOfWork.SaveChangesAsync(stoppingToken);
                await channel.BasicAckAsync(message.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                _logger.LogInformation("Job {JobId} completed on retry attempt {Attempt}.", job.Id, job.RetryCount);
                return;
            }

            errorMessage = retryResult.ErrorMessage;
            _logger.LogWarning("Job {JobId} retry attempt {Attempt} failed: {Error}", job.Id, job.RetryCount, errorMessage);
        }

        _logger.LogError("Job {JobId} exceeded max retries. Publishing to dead-letter queue.", job.Id);
        job.MarkAsFailed(errorMessage);
        await unitOfWork.SaveChangesAsync(stoppingToken);

        var headers = message.BasicProperties?.Headers;
        await deadLetterPublisher.PublishAsync(originalBody, $"Max retries exceeded: {errorMessage}", headers, stoppingToken);

        await channel.BasicAckAsync(message.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
    }
}
