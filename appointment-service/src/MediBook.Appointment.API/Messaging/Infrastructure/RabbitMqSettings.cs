namespace MediBook.Appointment.API.Messaging.Infrastructure;

/// <summary>
/// Strongly-typed RabbitMQ configuration for the Appointment Service.
/// Bind from appsettings.json → "RabbitMQ" section.
/// Topology constants must stay in sync with Schedule and Payment services.
/// </summary>
public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// Full CloudAMQP AMQP connection string.
    /// Format: amqps://user:password@host/vhost
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    // ── Topology constants (must match other services exactly) ───────────────

    public const string ExchangeName          = "medibook.saga";
    public const string PaymentSucceededKey   = "payment.succeeded";

    /// <summary>
    /// Dedicated queue for the Appointment Service to consume PaymentSucceeded.
    /// Uses a different queue name from the Schedule Service queue so both
    /// services get an independent copy of the event (fan-out via topic exchange).
    /// </summary>
    public const string AppointmentSucceededQueue = "queue.appointment.payment.succeeded";
}
