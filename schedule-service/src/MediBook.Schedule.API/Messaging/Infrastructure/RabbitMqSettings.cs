namespace MediBook.Schedule.API.Messaging.Infrastructure;

/// <summary>
/// Strongly-typed configuration for the RabbitMQ / CloudAMQP connection.
/// Bind from appsettings.json → "RabbitMQ" section.
/// </summary>
public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// Full CloudAMQP AMQP connection string.
    /// Format: amqps://user:password@host/vhost
    /// Example: amqps://abc:xyz@hawk.rmq.cloudamqp.com/abc
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    // ── Exchange / Queue topology constants ───────────────────────────────────

    /// <summary>Single topic exchange shared by both services.</summary>
    public const string ExchangeName = "medibook.saga";

    /// <summary>Routing key for the PaymentRequested event.</summary>
    public const string PaymentRequestedKey = "payment.requested";

    /// <summary>Routing key for the PaymentSucceeded event.</summary>
    public const string PaymentSucceededKey = "payment.succeeded";

    /// <summary>Routing key for the PaymentFailed event.</summary>
    public const string PaymentFailedKey = "payment.failed";

    /// <summary>Queue where the Payment Service listens for PaymentRequested.</summary>
    public const string PaymentRequestedQueue = "queue.payment.requested";

    /// <summary>Queue where the Schedule Service listens for PaymentSucceeded.</summary>
    public const string PaymentSucceededQueue = "queue.payment.succeeded";

    /// <summary>Queue where the Schedule Service listens for PaymentFailed.</summary>
    public const string PaymentFailedQueue = "queue.payment.failed";
}
