using System.Text;
using System.Text.Json;
using MediBook.Schedule.API.Data;
using MediBook.Schedule.API.Messaging.Contracts;
using MediBook.Schedule.API.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MediBook.Schedule.API.Messaging.Consumers;

/// <summary>
/// Background service that listens for payment outcome events and drives
/// the final steps of the Saga from the Schedule Service (Orchestrator) side.
///
/// Consumed queues:
///   queue.payment.succeeded  →  mark slot CONFIRMED  (IsBooked = true)
///   queue.payment.failed     →  mark slot AVAILABLE  (rollback / compensation)
///
/// Idempotency:
///   Before updating the slot we check its current state.  If the slot is
///   already in the target state (e.g. already booked when a duplicate
///   PaymentSucceeded arrives) we simply ack the message and skip.
///
/// Error handling:
///   Deserialization failures → Nack without requeue (dead-letter the message).
///   DB errors                → Nack with requeue=true so RabbitMQ redelivers.
/// </summary>
public sealed class PaymentResultConsumer : BackgroundService
{
    private readonly RabbitMqConnectionFactory    _connectionFactory;
    private readonly IServiceScopeFactory         _scopeFactory;
    private readonly ILogger<PaymentResultConsumer> _logger;

    private IModel? _channel;

    public PaymentResultConsumer(
        RabbitMqConnectionFactory      connectionFactory,
        IServiceScopeFactory           scopeFactory,
        ILogger<PaymentResultConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _scopeFactory      = scopeFactory;
        _logger            = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Blocking setup — run on thread-pool so we don't block the host startup
        return Task.Run(() => StartConsuming(stoppingToken), stoppingToken);
    }

    private void StartConsuming(CancellationToken stoppingToken)
    {
        var connection = _connectionFactory.GetConnection();
        _channel = connection.CreateModel();

        // Fair dispatch: process one message at a time per consumer
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        // ── Subscribe to PaymentSucceeded ─────────────────────────────────────
        var succeededConsumer = new EventingBasicConsumer(_channel);
        succeededConsumer.Received += (_, ea) =>
            HandleMessage<PaymentSucceeded>(ea, HandlePaymentSucceeded);

        _channel.BasicConsume(
            queue:       RabbitMqSettings.PaymentSucceededQueue,
            autoAck:     false,   // manual ack for reliability
            consumer:    succeededConsumer);

        // ── Subscribe to PaymentFailed ────────────────────────────────────────
        var failedConsumer = new EventingBasicConsumer(_channel);
        failedConsumer.Received += (_, ea) =>
            HandleMessage<PaymentFailed>(ea, HandlePaymentFailed);

        _channel.BasicConsume(
            queue:       RabbitMqSettings.PaymentFailedQueue,
            autoAck:     false,
            consumer:    failedConsumer);

        _logger.LogInformation(
            "[Schedule] PaymentResultConsumer started. " +
            "Listening on '{Succeeded}' and '{Failed}'.",
            RabbitMqSettings.PaymentSucceededQueue,
            RabbitMqSettings.PaymentFailedQueue);

        // Block until cancellation (host shutdown)
        stoppingToken.WaitHandle.WaitOne();

        _logger.LogInformation("[Schedule] PaymentResultConsumer stopping.");
        try { _channel.Close(); } catch { /* ignore */ }
    }

    // ── PaymentSucceeded handler ──────────────────────────────────────────────

    /// <summary>
    /// ★ SAGA SUCCESS — slot confirmed.
    /// Called when the Payment Service reports that payment went through.
    /// We update the slot: IsBooked = true (CONFIRMED state).
    /// </summary>
    private async Task HandlePaymentSucceeded(PaymentSucceeded @event)
    {
        _logger.LogInformation(
            "[Schedule] ★ SAGA SUCCESS — PaymentSucceeded received. " +
            "CorrelationId={CorrelationId}, SlotId={SlotId}, PaymentId={PaymentId}",
            @event.CorrelationId, @event.SlotId, @event.PaymentId);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        var slot = await db.Slots.FindAsync(@event.SlotId);
        if (slot is null)
        {
            _logger.LogWarning(
                "[Schedule] PaymentSucceeded: Slot {SlotId} not found — skipping.", @event.SlotId);
            return;
        }

        // ── Idempotency check ─────────────────────────────────────────────────
        if (slot.IsBooked)
        {
            _logger.LogInformation(
                "[Schedule] PaymentSucceeded: Slot {SlotId} already CONFIRMED — duplicate message, ignoring.",
                @event.SlotId);
            return;
        }

        // Slot should be in PENDING (IsBooked=false) state — now move to CONFIRMED
        slot.Book();
        await db.SaveChangesAsync();

        _logger.LogInformation(
            "[Schedule] Slot {SlotId} → CONFIRMED (IsBooked=true). CorrelationId={CorrelationId}",
            @event.SlotId, @event.CorrelationId);
    }

    // ── PaymentFailed handler (Compensation) ──────────────────────────────────

    /// <summary>
    /// ★ SAGA COMPENSATION — slot rolled back to AVAILABLE.
    /// Called when the Payment Service reports failure.
    /// We release the PENDING hold so the slot becomes bookable again.
    /// </summary>
    private async Task HandlePaymentFailed(PaymentFailed @event)
    {
        _logger.LogWarning(
            "[Schedule] ★ SAGA COMPENSATION — PaymentFailed received. " +
            "CorrelationId={CorrelationId}, SlotId={SlotId}, Reason={Reason}",
            @event.CorrelationId, @event.SlotId, @event.Reason);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        var slot = await db.Slots.FindAsync(@event.SlotId);
        if (slot is null)
        {
            _logger.LogWarning(
                "[Schedule] PaymentFailed: Slot {SlotId} not found — nothing to roll back.", @event.SlotId);
            return;
        }

        // ── Idempotency check ─────────────────────────────────────────────────
        // If the slot is already available (IsBooked=false) a previous
        // compensation already ran — skip safely.
        if (!slot.IsBooked)
        {
            _logger.LogInformation(
                "[Schedule] PaymentFailed: Slot {SlotId} is already AVAILABLE — duplicate message, ignoring.",
                @event.SlotId);
            return;
        }

        // Compensating action: release the slot back to AVAILABLE
        slot.Unbook();
        await db.SaveChangesAsync();

        _logger.LogInformation(
            "[Schedule] ★ Compensation complete — Slot {SlotId} → AVAILABLE. CorrelationId={CorrelationId}",
            @event.SlotId, @event.CorrelationId);
    }

    // ── Generic message dispatch ──────────────────────────────────────────────

    private void HandleMessage<T>(BasicDeliverEventArgs ea, Func<T, Task> handler)
    {
        var body = Encoding.UTF8.GetString(ea.Body.Span);
        T? @event;

        try
        {
            @event = JsonSerializer.Deserialize<T>(body);
            if (@event is null) throw new InvalidOperationException("Deserialized to null.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Schedule] Failed to deserialize message from '{Queue}'. " +
                "Body: {Body} — Nacking without requeue (dead-letter).",
                ea.RoutingKey, body);

            // Poison message — do not requeue; send to dead-letter queue if configured
            _channel?.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        try
        {
            handler(@event).GetAwaiter().GetResult();
            _channel?.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Schedule] Error processing message for {EventType}. " +
                "Will requeue for retry.", typeof(T).Name);

            // Transient error (DB timeout etc.) — requeue for another attempt
            _channel?.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    public override void Dispose()
    {
        try { _channel?.Close(); } catch { /* ignore */ }
        _channel?.Dispose();
        base.Dispose();
    }
}
