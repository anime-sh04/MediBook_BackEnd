using System.Text;
using System.Text.Json;
using MediBook.Schedule.API.Messaging.Contracts;
using RabbitMQ.Client;

namespace MediBook.Schedule.API.Messaging.Infrastructure;

/// <summary>
/// Publishes Saga events to the RabbitMQ topic exchange.
///
/// Only the Schedule Service (Orchestrator) publishes — it sends PaymentRequested.
/// Uses a dedicated channel per publish call so it is safe to call from any
/// thread / async context (IModel is not thread-safe).
/// </summary>
public sealed class SagaEventPublisher
{
    private readonly RabbitMqConnectionFactory    _connectionFactory;
    private readonly ILogger<SagaEventPublisher> _logger;

    public SagaEventPublisher(
        RabbitMqConnectionFactory    connectionFactory,
        ILogger<SagaEventPublisher> logger)
    {
        _connectionFactory = connectionFactory;
        _logger            = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// ★ SAGA START — publishes PaymentRequested to kick off the Saga.
    /// Called from ScheduleService.InitiateBookingAsync after the slot
    /// is marked PENDING in the database.
    /// </summary>
    public void PublishPaymentRequested(PaymentRequested @event)
    {
        Publish(RabbitMqSettings.PaymentRequestedKey, @event);

        _logger.LogInformation(
            "[Schedule] ★ SAGA START — Published PaymentRequested. " +
            "CorrelationId={CorrelationId}, SlotId={SlotId}, Amount={Amount}",
            @event.CorrelationId, @event.SlotId, @event.Amount);
    }

    // ── Core publish ──────────────────────────────────────────────────────────

    private void Publish<T>(string routingKey, T @event)
    {
        var connection = _connectionFactory.GetConnection();
        using var channel = connection.CreateModel();

        // Mark messages as persistent so they survive a broker restart
        var props = channel.CreateBasicProperties();
        props.Persistent   = true;
        props.ContentType  = "application/json";
        props.DeliveryMode = 2; // persistent

        var json  = JsonSerializer.Serialize(@event);
        var body  = Encoding.UTF8.GetBytes(json);

        channel.BasicPublish(
            exchange:    RabbitMqSettings.ExchangeName,
            routingKey:  routingKey,
            basicProperties: props,
            body:        body);

        _logger.LogDebug(
            "[Schedule] Published '{RoutingKey}' ({Bytes} bytes) to exchange '{Exchange}'.",
            routingKey, body.Length, RabbitMqSettings.ExchangeName);
    }
}
