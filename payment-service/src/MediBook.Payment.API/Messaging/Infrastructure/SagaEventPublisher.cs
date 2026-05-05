using System.Text;
using System.Text.Json;
using MediBook.Payment.API.Messaging.Contracts;
using RabbitMQ.Client;

namespace MediBook.Payment.API.Messaging.Infrastructure;

/// <summary>
/// Publishes payment outcome events (PaymentSucceeded / PaymentFailed)
/// back to the topic exchange so the Schedule Service can react.
///
/// Called from the PaymentRequestedConsumer after mock payment processing.
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
    /// ★ SAGA SUCCESS — publishes PaymentSucceeded so the Schedule Service
    /// can mark the slot CONFIRMED (IsBooked = true).
    /// </summary>
    public void PublishPaymentSucceeded(PaymentSucceeded @event)
    {
        Publish(RabbitMqSettings.PaymentSucceededKey, @event);

        _logger.LogInformation(
            "[Payment] ★ SAGA SUCCESS — Published PaymentSucceeded. " +
            "CorrelationId={CorrelationId}, SlotId={SlotId}, PaymentId={PaymentId}",
            @event.CorrelationId, @event.SlotId, @event.PaymentId);
    }

    /// <summary>
    /// ★ SAGA FAILURE — publishes PaymentFailed so the Schedule Service
    /// can roll the slot back to AVAILABLE (compensation transaction).
    /// </summary>
    public void PublishPaymentFailed(PaymentFailed @event)
    {
        Publish(RabbitMqSettings.PaymentFailedKey, @event);

        _logger.LogWarning(
            "[Payment] ★ SAGA FAILURE — Published PaymentFailed. " +
            "CorrelationId={CorrelationId}, SlotId={SlotId}, Reason={Reason}",
            @event.CorrelationId, @event.SlotId, @event.Reason);
    }

    // ── Core publish ──────────────────────────────────────────────────────────

    private void Publish<T>(string routingKey, T @event)
    {
        var connection = _connectionFactory.GetConnection();
        using var channel = connection.CreateModel();

        var props = channel.CreateBasicProperties();
        props.Persistent   = true;
        props.ContentType  = "application/json";
        props.DeliveryMode = 2;

        var json = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(json);

        channel.BasicPublish(
            exchange:        RabbitMqSettings.ExchangeName,
            routingKey:      routingKey,
            basicProperties: props,
            body:            body);

        _logger.LogDebug(
            "[Payment] Published '{RoutingKey}' ({Bytes} bytes) to exchange '{Exchange}'.",
            routingKey, body.Length, RabbitMqSettings.ExchangeName);
    }
}
