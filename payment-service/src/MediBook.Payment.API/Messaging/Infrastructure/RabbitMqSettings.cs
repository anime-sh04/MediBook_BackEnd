namespace MediBook.Payment.API.Messaging.Infrastructure;

/// <summary>
/// Strongly-typed configuration for RabbitMQ / CloudAMQP.
/// Bind from appsettings.json → "RabbitMQ" section.
/// Topology constants must stay in sync with Schedule Service.
/// </summary>
public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// Full CloudAMQP AMQP connection string.
    /// Format: amqps://user:password@host/vhost
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    // ── Topology constants (must match Schedule Service exactly) ─────────────

    public const string ExchangeName            = "medibook.saga";
    public const string PaymentRequestedKey     = "payment.requested";
    public const string PaymentSucceededKey     = "payment.succeeded";
    public const string PaymentFailedKey        = "payment.failed";
    public const string PaymentRequestedQueue   = "queue.payment.requested";
    public const string PaymentSucceededQueue   = "queue.payment.succeeded";
    public const string PaymentFailedQueue      = "queue.payment.failed";
}
