using System.Text;
using System.Text.Json;
using MediBook.Appointment.API.DTOs;
using MediBook.Appointment.API.Messaging.Contracts;
using MediBook.Appointment.API.Messaging.Infrastructure;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MediBook.Appointment.API.Messaging.Consumers;

/// <summary>
/// ════════════════════════════════════════════════════════════════════════════
///  SAGA PARTICIPANT — PaymentSucceededConsumer  (Appointment Service)
/// ════════════════════════════════════════════════════════════════════════════
///
/// Responsibility:
///   Listens for PaymentSucceeded events and creates an Appointment record
///   by delegating to IAppointmentService.CreateFromSagaAsync.
///
///   This is the ONLY trigger for appointment creation.
///   The HTTP POST /appointments endpoint has been removed.
///
/// Design:
///   • No calls to Schedule Service or Payment Service.
///   • All data needed to create the appointment arrives in the event.
///   • Idempotent — safe to deliver the event more than once.
///
/// Error handling:
///   Deserialization failure → Nack without requeue (dead-letter).
///   Service / DB error     → Nack with requeue=true (transient retry).
///
/// Queue: queue.appointment.payment.succeeded
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public sealed class PaymentSucceededConsumer : BackgroundService
{
    private readonly RabbitMqConnectionFactory          _connectionFactory;
    private readonly IServiceScopeFactory               _scopeFactory;
    private readonly ILogger<PaymentSucceededConsumer>  _logger;

    private IModel? _channel;

    public PaymentSucceededConsumer(
        RabbitMqConnectionFactory         connectionFactory,
        IServiceScopeFactory              scopeFactory,
        ILogger<PaymentSucceededConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _scopeFactory      = scopeFactory;
        _logger            = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => StartConsuming(stoppingToken), stoppingToken);

    private void StartConsuming(CancellationToken stoppingToken)
    {
        var connection = _connectionFactory.GetConnection();
        _channel = connection.CreateModel();

        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (_, ea) =>
            HandleMessage<PaymentSucceeded>(ea, HandlePaymentSucceeded);

        _channel.BasicConsume(
            queue:    RabbitMqSettings.AppointmentSucceededQueue,
            autoAck:  false,
            consumer: consumer);

        _logger.LogInformation(
            "[Appointment] PaymentSucceededConsumer started — listening on '{Queue}'.",
            RabbitMqSettings.AppointmentSucceededQueue);

        stoppingToken.WaitHandle.WaitOne();

        _logger.LogInformation("[Appointment] PaymentSucceededConsumer stopping.");
        try { _channel.Close(); } catch { /* ignore on shutdown */ }
    }

    // ── Core handler ──────────────────────────────────────────────────────────

    private async Task HandlePaymentSucceeded(PaymentSucceeded @event)
    {
        _logger.LogInformation(
            "[Appointment] ★ SAGA — PaymentSucceeded received. " +
            "CorrelationId={CorrelationId}, SlotId={SlotId}, PaymentId={PaymentId}",
            @event.CorrelationId, @event.SlotId, @event.PaymentId);

        using var scope    = _scopeFactory.CreateScope();
        var appointmentSvc = scope.ServiceProvider
            .GetRequiredService<Services.IAppointmentService>();

        var command = new CreateAppointmentFromSagaCommand(
            PatientId:          @event.PatientId,
            ProviderId:         @event.ProviderId,
            SlotId:             @event.SlotId,
            ServiceType:        @event.ServiceType,
            AppointmentDate:    @event.AppointmentDate,
            StartTime:          @event.StartTime,
            EndTime:            @event.EndTime,
            ModeOfConsultation: @event.ModeOfConsultation,
            CorrelationId:      @event.CorrelationId,
            Notes:              @event.Notes);

        await appointmentSvc.CreateFromSagaAsync(command);
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
                "[Appointment] Failed to deserialize message. Body: {Body} — Nacking without requeue.", body);
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
                "[Appointment] Error processing {EventType} — requeuing for retry.", typeof(T).Name);
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
