using System.Text.Json;
using JobFlow.Application.Interfaces;
using JobFlow.Contracts.Messages;
using RabbitMQ.Client;
using Polly;

namespace JobFlow.Infrastructure.Services;

public sealed class RabbitMqJobPublisher : IJobPublisher
{
    private readonly IConnection _connection;
    private readonly ResiliencePipeline _resiliencePipeline;

    public RabbitMqJobPublisher(IConnection connection, ResiliencePipeline resiliencePipeline)
    {
        _connection = connection;
        _resiliencePipeline = resiliencePipeline;
    }

    public async Task PublishJobCreatedAsync(JobCreatedMessage message, CancellationToken cancellationToken = default)
    {
        await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            await using var channel = await _connection.CreateChannelAsync(new CreateChannelOptions(false, false, null, 0), ct);
            await channel.ExchangeDeclareAsync("jobflow.exchange", ExchangeType.Direct, durable: true, autoDelete: false, arguments: null, passive: false, noWait: false, cancellationToken: ct);
            await channel.QueueDeclareAsync("jobflow.job-created", durable: true, exclusive: false, autoDelete: false, arguments: null, passive: false, noWait: false, cancellationToken: ct);
            await channel.QueueBindAsync("jobflow.job-created", "jobflow.exchange", "job.created", arguments: null, noWait: false, cancellationToken: ct);

            var payload = JsonSerializer.SerializeToUtf8Bytes(message);
            var properties = new BasicProperties();
            properties.Persistent = true;
            properties.Headers = new Dictionary<string, object?>
            {
                ["CorrelationId"] = message.CorrelationId,
                ["TraceId"] = message.TraceId
            };

            await channel.BasicPublishAsync(
                exchange: "jobflow.exchange",
                routingKey: "job.created",
                mandatory: false,
                basicProperties: properties,
                body: payload,
                cancellationToken: ct);
        }, cancellationToken);
    }
}
