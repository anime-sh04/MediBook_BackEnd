using RabbitMQ.Client;

namespace MediBook.Schedule.API.Messaging.Infrastructure;

/// <summary>
/// Declares the RabbitMQ exchange and all queues used by the Saga.
/// Called once at application startup (inside Program.cs) so both services
/// share the exact same topology regardless of start order.
///
/// Topology:
///   Exchange : medibook.saga  (topic, durable)
///   Queues   : queue.payment.requested  → binds routing key "payment.requested"
///              queue.payment.succeeded  → binds routing key "payment.succeeded"
///              queue.payment.failed     → binds routing key "payment.failed"
/// </summary>
public static class RabbitMqTopology
{
    public static void DeclareAll(IConnection connection, ILogger logger)
    {
        using var channel = connection.CreateModel();

        // ── Exchange (topic) — durable so it survives broker restart ──────────
        channel.ExchangeDeclare(
            exchange:    RabbitMqSettings.ExchangeName,
            type:        ExchangeType.Topic,
            durable:     true,
            autoDelete:  false);

        logger.LogInformation("[Schedule] Exchange '{Exchange}' declared.", RabbitMqSettings.ExchangeName);

        // ── Helper to declare a queue and bind it to the exchange ─────────────
        void DeclareAndBind(string queue, string routingKey)
        {
            channel.QueueDeclare(
                queue:      queue,
                durable:    true,       // survive broker restart
                exclusive:  false,
                autoDelete: false,
                arguments:  null);

            channel.QueueBind(
                queue:      queue,
                exchange:   RabbitMqSettings.ExchangeName,
                routingKey: routingKey);

            logger.LogInformation(
                "[Schedule] Queue '{Queue}' declared and bound to '{Key}'.", queue, routingKey);
        }

        // Queue consumed by Payment Service
        DeclareAndBind(RabbitMqSettings.PaymentRequestedQueue,  RabbitMqSettings.PaymentRequestedKey);

        // Queues consumed by Schedule Service (responses from Payment Service)
        DeclareAndBind(RabbitMqSettings.PaymentSucceededQueue,  RabbitMqSettings.PaymentSucceededKey);
        DeclareAndBind(RabbitMqSettings.PaymentFailedQueue,     RabbitMqSettings.PaymentFailedKey);
    }
}
