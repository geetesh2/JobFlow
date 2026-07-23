using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace JobFlow.Worker.Messaging;

public sealed class DeadLetterPublisher
{
    private readonly IConnection _connection;
    private readonly ILogger<DeadLetterPublisher> _logger;

    private const string DlxExchange = "jobflow.dlx";
    private const string DeadLetterQueue = "jobflow.dead-letter";

    public DeadLetterPublisher(IConnection connection, ILogger<DeadLetterPublisher> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task PublishAsync(
        byte[] originalMessage,
        string reason,
        IDictionary<string, object?>? originalHeaders,
        CancellationToken ct = default)
    {
        using var channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(DlxExchange, ExchangeType.Fanout, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(DeadLetterQueue, DlxExchange, routingKey: string.Empty, cancellationToken: ct);

        var properties = new BasicProperties
        {
            Persistent = true,
            Headers = new Dictionary<string, object?>
            {
                ["x-death-reason"] = reason
            }
        };

        if (originalHeaders is not null)
        {
            foreach (var header in originalHeaders)
            {
                properties.Headers.TryAdd($"x-original-{header.Key}", header.Value);
            }
        }

        await channel.BasicPublishAsync(DlxExchange, routingKey: string.Empty, mandatory: false, properties, originalMessage, ct);

        _logger.LogWarning("Published message to dead-letter queue. Reason: {Reason}", reason);
    }
}
