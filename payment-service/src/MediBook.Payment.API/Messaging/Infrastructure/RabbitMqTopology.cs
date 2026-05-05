using RabbitMQ.Client;

namespace MediBook.Payment.API.Messaging.Infrastructure;

/// <summary>
/// Declares the shared exchange and all Saga queues.
/// Called on startup — safe to call multiple times (idempotent by default).
/// The Payment Service declares the same topology so it works regardless of
/// which service starts first.
/// </summary>
public static class RabbitMqTopology
{
    public static void DeclareAll(IConnection connection, ILogger logger)
    {
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(
            exchange:   RabbitMqSettings.ExchangeName,
            type:       ExchangeType.Topic,
            durable:    true,
            autoDelete: false);

        logger.LogInformation("[Payment] Exchange '{Exchange}' declared.", RabbitMqSettings.ExchangeName);

        void DeclareAndBind(string queue, string routingKey)
        {
            channel.QueueDeclare(
                queue:      queue,
                durable:    true,
                exclusive:  false,
                autoDelete: false,
                arguments:  null);

            channel.QueueBind(
                queue:      queue,
                exchange:   RabbitMqSettings.ExchangeName,
                routingKey: routingKey);

            logger.LogInformation(
                "[Payment] Queue '{Queue}' declared and bound to '{Key}'.", queue, routingKey);
        }

        DeclareAndBind(RabbitMqSettings.PaymentRequestedQueue, RabbitMqSettings.PaymentRequestedKey);
        DeclareAndBind(RabbitMqSettings.PaymentSucceededQueue, RabbitMqSettings.PaymentSucceededKey);
        DeclareAndBind(RabbitMqSettings.PaymentFailedQueue,    RabbitMqSettings.PaymentFailedKey);
    }
}
