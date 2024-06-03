using RabbitMQ.Client;

namespace Shared.RabbitMQ.Interfaces;

public interface IRabbitMqUtil
{
    Task ListenMessageQueue(IModel channel, CancellationToken cancellationToken);
    Task PublishMessageQueue(string routingKey, string eventData);
}