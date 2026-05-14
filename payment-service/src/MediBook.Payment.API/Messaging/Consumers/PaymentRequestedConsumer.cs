using System.Text;
using System.Text.Json;
using MediBook.Payment.API.Data;
using MediBook.Payment.API.Messaging.Contracts;
using MediBook.Payment.API.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Razorpay.Api;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MediBook.Payment.API.Messaging.Consumers;

/// <summary>
/// ════════════════════════════════════════════════════════════════════════════
///  SAGA PARTICIPANT — PaymentRequestedConsumer  (Payment Service)
/// ════════════════════════════════════════════════════════════════════════════
///
/// Responsibility:
///   Receives the PaymentRequested event published by the Schedule Service,
///   creates a Razorpay order, persists a PENDING payment record, and
///   returns control to the frontend — it does NOT complete or mock the payment.
///
/// What this consumer does NOT do (by design):
///   ✗ Does NOT call payment.MarkPaid()
///   ✗ Does NOT publish PaymentSucceeded / PaymentFailed
///   ✗ Does NOT simulate or auto-complete the payment
///
/// The Saga outcome is driven exclusively by human interaction:
///   • User pays  → frontend calls POST /payments/confirm → PaymentSucceeded published
///   • User bails → frontend calls POST /payments/fail    → PaymentFailed published
///
/// Flow:
///   1. Receive PaymentRequested event
///   2. Idempotency check (duplicate messages are safely skipped)
///   3. Create Razorpay order via SDK
///   4. Persist Payment row (Status=Pending, CorrelationId, SlotId, appointment fields stored)
///   5. Ack message — frontend polls GET /payments/slot/{slotId} for orderId
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public sealed class PaymentRequestedConsumer : BackgroundService
{
    private readonly RabbitMqConnectionFactory         _connectionFactory;
    private readonly IServiceScopeFactory              _scopeFactory;
    private readonly ILogger<PaymentRequestedConsumer> _logger;

    private IModel? _channel;

    public PaymentRequestedConsumer(
        RabbitMqConnectionFactory         connectionFactory,
        IServiceScopeFactory              scopeFactory,
        ILogger<PaymentRequestedConsumer> logger)
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
            HandleMessage<PaymentRequested>(ea, HandlePaymentRequested);

        _channel.BasicConsume(
            queue:    RabbitMqSettings.PaymentRequestedQueue,
            autoAck:  false,
            consumer: consumer);

        _logger.LogInformation(
            "[Payment] PaymentRequestedConsumer started — listening on '{Queue}'.",
            RabbitMqSettings.PaymentRequestedQueue);

        stoppingToken.WaitHandle.WaitOne();

        _logger.LogInformation("[Payment] PaymentRequestedConsumer stopping.");
        try { _channel.Close(); } catch { /* ignore on shutdown */ }
    }

    // ── Core handler ──────────────────────────────────────────────────────────

    private async Task HandlePaymentRequested(PaymentRequested @event)
    {
        _logger.LogInformation(
            "[Payment] ★ SAGA — PaymentRequested received. " +
            "CorrelationId={CorrelationId}, SlotId={SlotId}, Amount={Amount}, Mode={Mode}",
            @event.CorrelationId, @event.SlotId, @event.Amount, @event.Mode);

        using var scope = _scopeFactory.CreateScope();
        var db               = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var razorpaySettings = scope.ServiceProvider.GetRequiredService<Helpers.RazorpaySettings>();

        // ── Idempotency check ─────────────────────────────────────────────────
        var existing = await db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CorrelationId == @event.CorrelationId);

        if (existing is not null)
        {
            _logger.LogInformation(
                "[Payment] Idempotency hit — CorrelationId={CorrelationId} already has " +
                "PaymentId={PaymentId} (Status={Status}). Skipping duplicate.",
                @event.CorrelationId, existing.PaymentId, existing.Status);
            return;
        }

        var mode = NormaliseMode(@event.Mode);

        // ── Create Razorpay order (online modes only) ─────────────────────────
        string razorpayOrderId = string.Empty;

        if (mode != Entities.Payment.ModeCash)
        {
            razorpayOrderId = CreateRazorpayOrder(
                razorpaySettings, @event.Amount, @event.Currency,
                @event.SlotId, @event.PatientId, _logger);
        }

        // ── Persist PENDING payment with appointment pass-through fields ──────
        var payment = Entities.Payment.Create(
            appointmentId:     @event.SlotId,
            patientId:         @event.PatientId,
            providerId:        @event.ProviderId,
            amount:            @event.Amount,
            mode:              mode,
            correlationId:     @event.CorrelationId,
            slotId:            @event.SlotId,
            currency:          @event.Currency,
            notes:             @event.Notes?.Trim(),
            // Pass appointment fields through so ConfirmPaymentAsync can echo
            // them in the PaymentSucceeded event without any HTTP calls.
            appointmentDate:    @event.AppointmentDate,
            startTime:          @event.StartTime,
            endTime:            @event.EndTime,
            serviceType:        @event.ServiceType,
            modeOfConsultation: @event.ModeOfConsultation);

        if (!string.IsNullOrEmpty(razorpayOrderId))
            SetRazorpayOrderId(payment, razorpayOrderId);

        db.Payments.Add(payment);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException dbEx)
            when (dbEx.InnerException?.Message.Contains("unique",    StringComparison.OrdinalIgnoreCase) == true
               || dbEx.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogInformation(
                "[Payment] Duplicate DB constraint for SlotId={SlotId} — treating as idempotent.",
                @event.SlotId);
            return;
        }

        _logger.LogInformation(
            "[Payment] PENDING payment created. PaymentId={PaymentId}, " +
            "RazorpayOrderId='{RazorpayOrderId}', CorrelationId={CorrelationId}. " +
            "Awaiting user action via Razorpay checkout.",
            payment.PaymentId, razorpayOrderId, @event.CorrelationId);
    }

    // ── Razorpay order creation ───────────────────────────────────────────────

    private static string CreateRazorpayOrder(
        Helpers.RazorpaySettings settings,
        decimal                  amount,
        string                   currency,
        int                      slotId,
        Guid                     patientId,
        ILogger                  logger)
    {
        var amountInPaise = (long)(amount * 100);
        var client        = new RazorpayClient(settings.KeyId, settings.KeySecret);

        var options = new Dictionary<string, object>
        {
            { "amount",   amountInPaise },
            { "currency", currency.ToUpperInvariant() },
            { "receipt",  $"slot_{slotId}_{DateTime.UtcNow:yyyyMMddHHmmss}" },
            { "notes", new Dictionary<string, string>
              {
                  { "slot_id",    slotId.ToString()    },
                  { "patient_id", patientId.ToString() }
              }
            }
        };

        Order order;
        try
        {
            order = client.Order.Create(options);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[Payment] Razorpay order creation failed for SlotId={SlotId}", slotId);
            throw new InvalidOperationException(
                "Payment gateway error while creating Razorpay order.", ex);
        }

        string orderId = order["id"]?.ToString() ?? string.Empty;

        logger.LogInformation(
            "[Payment] Razorpay order created. OrderId={OrderId}, SlotId={SlotId}",
            orderId, slotId);

        return orderId;
    }

    private static void SetRazorpayOrderId(Entities.Payment payment, string razorpayOrderId)
        => payment.SetPendingRazorpayOrderId(razorpayOrderId);

    private static string NormaliseMode(string mode) => mode?.Trim() switch
    {
        "UPI"    or "upi"    => Entities.Payment.ModeUpi,
        "Wallet" or "wallet" => Entities.Payment.ModeWallet,
        "Cash"   or "cash"   => Entities.Payment.ModeCash,
        _                    => Entities.Payment.ModeCard
    };

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
                "[Payment] Failed to deserialize message. Body: {Body} — Nacking without requeue.", body);
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
            File.AppendAllText(
                @"C:\home\LogFiles\payment-errors.txt",
                $"\n\n[{DateTime.UtcNow}]\n{ex}\n");
        
            Console.WriteLine("===== PAYMENT ERROR =====");
            Console.WriteLine(ex.ToString());
        
            _channel?.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    public override void Dispose()
    {
        try { _channel?.Close(); } catch { /* ignore */ }
        _channel?.Dispose();
        base.Dispose();
    }
}
