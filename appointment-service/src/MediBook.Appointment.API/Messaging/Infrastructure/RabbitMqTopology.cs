using RabbitMQ.Client;

namespace MediBook.Appointment.API.Messaging.Infrastructure;

/// <summary>
/// Declares the exchange and Appointment Service's own queue on startup.
///
/// The exchange (medibook.saga) is shared with Schedule and Payment services.
/// Declaring it here is idempotent — if it already exists with the same
/// parameters, RabbitMQ silently no-ops.
///
/// The Appointment Service binds its own queue
/// ("queue.appointment.payment.succeeded") to the "payment.succeeded"
/// routing key so it receives an independent copy of every PaymentSucceeded
/// event alongside the Schedule Service's queue.
/// </summary>
public static class RabbitMqTopology
{
    public static void DeclareAll(IConnection connection, ILogger logger)
    {
        using var channel = connection.CreateModel();

        // ── Shared topic exchange ─────────────────────────────────────────────
        channel.ExchangeDeclare(
            exchange:   RabbitMqSettings.ExchangeName,
            type:       ExchangeType.Topic,
            durable:    true,
            autoDelete: false);

        logger.LogInformation(
            "[Appointment] Exchange '{Exchange}' declared.", RabbitMqSettings.ExchangeName);

        // ── Appointment Service's dedicated PaymentSucceeded queue ────────────
        channel.QueueDeclare(
            queue:      RabbitMqSettings.AppointmentSucceededQueue,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            arguments:  null);

        channel.QueueBind(
            queue:      RabbitMqSettings.AppointmentSucceededQueue,
            exchange:   RabbitMqSettings.ExchangeName,
            routingKey: RabbitMqSettings.PaymentSucceededKey);

        logger.LogInformation(
            "[Appointment] Queue '{Queue}' declared and bound to '{Key}'.",
            RabbitMqSettings.AppointmentSucceededQueue,
            RabbitMqSettings.PaymentSucceededKey);
    }
}
