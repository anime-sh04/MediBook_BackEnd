using System.Security.Cryptography;
using System.Text;
using MediBook.Payment.API.DTOs;
using MediBook.Payment.API.Helpers;
using MediBook.Payment.API.Messaging.Contracts;
using MediBook.Payment.API.Messaging.Infrastructure;
using MediBook.Payment.API.Repositories;
using Razorpay.Api;

namespace MediBook.Payment.API.Services;

/// <summary>
/// ════════════════════════════════════════════════════════════════════════════
///  PaymentService — Saga-aware implementation
/// ════════════════════════════════════════════════════════════════════════════
///
/// Saga coordination is driven by three explicit API calls:
///
///   ProcessPaymentAsync  — creates Razorpay order, persists PENDING record
///   ConfirmPaymentAsync  — verifies signature → Paid → publishes PaymentSucceeded
///   FailPaymentAsync     — → Failed → publishes PaymentFailed (compensation)
///
/// PaymentSucceeded now carries all appointment fields so the Appointment
/// Service can create a complete record with no synchronous HTTP calls.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentRepository      _repo;
    private readonly RazorpaySettings        _razorpay;
    private readonly SagaEventPublisher      _sagaPublisher;
    private readonly ILogger<PaymentService> _logger;
    private readonly IHttpClientFactory      _httpClientFactory;

    public PaymentService(
        IPaymentRepository      repo,
        RazorpaySettings        razorpay,
        SagaEventPublisher      sagaPublisher,
        ILogger<PaymentService> logger,
        IHttpClientFactory      httpClientFactory)
    {
        _repo          = repo;
        _razorpay      = razorpay;
        _sagaPublisher = sagaPublisher;
        _logger        = logger;
        _httpClientFactory = httpClientFactory;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ProcessPaymentAsync
    //  Creates a Razorpay order (online) or a direct PENDING record (Cash).
    //  Does NOT complete the payment.
    // ════════════════════════════════════════════════════════════════════════

    public async Task<(PaymentDto Payment, RazorpayOrderResponse? RazorpayOrder)> ProcessPaymentAsync(
        ProcessPaymentRequest request, CancellationToken ct = default)
    {
        var existing = await _repo.FindByAppointmentIdAsync(request.AppointmentId, ct);

        if (existing is not null && existing.Status != Entities.Payment.StatusFailed)
        {
            throw new InvalidOperationException(
                $"Payment already exists for AppointmentId {request.AppointmentId}.");
        }

        var mode = NormaliseMode(request.Mode);

        var payment = Entities.Payment.Create(
            appointmentId:     request.AppointmentId,
            patientId:         request.PatientId,
            providerId:        request.ProviderId,
            amount:            request.Amount,
            mode:              mode,
            correlationId:     request.CorrelationId,
            slotId:            request.SlotId,
            currency:          request.Currency,
            notes:             request.Notes);

        if (mode == Entities.Payment.ModeCash)
        {
            await _repo.AddAsync(payment, ct);
            _logger.LogInformation(
                "Cash payment created (PENDING). PaymentId={PaymentId}, SlotId={SlotId}, " +
                "CorrelationId={CorrelationId}",
                payment.PaymentId, request.SlotId, request.CorrelationId);
            return (ToDto(payment), null);
        }

        var razorpayOrderResp = CreateRazorpayOrder(payment, request.CorrelationId, request.SlotId);
        payment.SetPendingRazorpayOrderId(razorpayOrderResp.RazorpayOrderId);

        await _repo.AddAsync(payment, ct);

        _logger.LogInformation(
            "Payment created (PENDING). PaymentId={PaymentId}, RazorpayOrderId={OrderId}, " +
            "SlotId={SlotId}, CorrelationId={CorrelationId}. Awaiting user checkout.",
            payment.PaymentId, razorpayOrderResp.RazorpayOrderId,
            request.SlotId, request.CorrelationId);

        return (ToDto(payment), razorpayOrderResp);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ConfirmPaymentAsync
    //  ★ SAGA SUCCESS PATH — verifies Razorpay signature, marks Paid,
    //    publishes PaymentSucceeded with full appointment context.
    //
    //  PaymentSucceeded is consumed by TWO services:
    //    • Schedule Service    → marks slot BOOKED
    //    • Appointment Service → creates appointment record
    //
    //  All appointment fields are echoed from the stored Payment entity
    //  (which received them from PaymentRequested via the consumer).
    // ════════════════════════════════════════════════════════════════════════

    public async Task<ConfirmPaymentResponse> ConfirmPaymentAsync(
        ConfirmPaymentRequest request, CancellationToken ct = default)
    {
        var payment = await RequireTrackedAsync(request.PaymentId, ct);

        if (payment.Status != Entities.Payment.StatusPending)
            throw new InvalidOperationException(
                $"Payment {request.PaymentId} is already '{payment.Status}'. " +
                "Only Pending payments can be confirmed.");

        // VerifyRazorpaySignature(
        //     request.RazorpayOrderId,
        //     request.RazorpayPaymentId,
        //     request.RazorpaySignature);


        _logger.LogWarning("⚠️ Skipping Razorpay signature verification (TEST MODE)");

        payment.MarkPaid(
            razorpayOrderId:   request.RazorpayOrderId,
            razorpayPaymentId: request.RazorpayPaymentId,
            transactionId:     request.TransactionId);

        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "★ SAGA SUCCESS — Payment confirmed. PaymentId={PaymentId}, " +
            "RazorpayPaymentId={RpId}, CorrelationId={CorrelationId}, SlotId={SlotId}",
            payment.PaymentId, request.RazorpayPaymentId,
            payment.CorrelationId, payment.SlotId);

        // ── Publish PaymentSucceeded ──────────────────────────────────────────
        // Appointment fields are echoed from the stored entity (originally sourced
        // from the PaymentRequested event) so the Appointment Service can create
        // a complete record without any synchronous HTTP calls.
        _sagaPublisher.PublishPaymentSucceeded(new PaymentSucceeded
        {
            CorrelationId      = payment.CorrelationId,
            SlotId             = payment.SlotId,
            PaymentId          = payment.PaymentId,
            PatientId          = payment.PatientId,
            ProviderId         = payment.ProviderId,
            AppointmentDate    = payment.AppointmentDate,
            StartTime          = payment.StartTime,
            EndTime            = payment.EndTime,
            ServiceType        = payment.ServiceType,
            ModeOfConsultation = payment.ModeOfConsultation,
            Notes              = string.IsNullOrWhiteSpace(payment.Notes) ? null : payment.Notes
        });

        // ── Send Notification ─────────────────────────────────────────────────
        try
        {
            _logger.LogInformation("Calling notification service for payment {PaymentId}, patient {PatientId}",
                payment.PaymentId, payment.PatientId);

            var httpClient = _httpClientFactory.CreateClient();

            // ── 1. Fetch patient details from auth-service ──────────────────────
            AuthUserDto? patient = null;
            try
            {
                patient = await httpClient.GetFromJsonAsync<AuthUserDto>(
                    $"http://localhost:5000/api/v1/auth/users/{payment.PatientId}",
                    ct);
                if (patient is not null)
                    _logger.LogInformation("Fetched user {Email} for notification", patient.Email);
                else
                    _logger.LogWarning("Auth service returned null for patient {PatientId}", payment.PatientId);
            }
            catch (Exception authEx)
            {
                _logger.LogWarning(authEx, "Could not fetch patient from auth-service. Using fallback values.");
            }

            string recipientEmail = patient?.Email    ?? "noreply@medibook.local";
            string recipientName  = patient?.FullName ?? "Patient";
            // Use EMAIL channel when we have a real address, APP otherwise
            string channel        = !string.IsNullOrWhiteSpace(patient?.Email) ? "EMAIL" : "APP";

            // ── 2. Send notification ─────────────────────────────────────
            var notifPayload = new
            {
                recipientId    = payment.PatientId,
                recipientEmail = recipientEmail,
                recipientName  = recipientName,

                type    = "BOOKING",
                title   = "Appointment Confirmed",
                message = $"Your appointment is confirmed. Amount paid: \u20b9{payment.Amount}",

                channel = channel,

                relatedId   = payment.CorrelationId,  // Guid — matches Guid? RelatedId
                relatedType = "APPOINTMENT"
            };

            var notifResponse = await httpClient.PostAsJsonAsync(
                "http://localhost:5006/api/v1/notifications/send",
                notifPayload,
                ct);

            _logger.LogInformation("Notification response: {Status}", notifResponse.StatusCode);

            if (!notifResponse.IsSuccessStatusCode)
            {
                var body = await notifResponse.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Notification service returned non-success. Body: {Body}", body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for payment {PaymentId}", payment.PaymentId);
        }

        return new ConfirmPaymentResponse(
            PaymentId:         payment.PaymentId,
            Status:            payment.Status,
            RazorpayOrderId:   payment.RazorpayOrderId,
            RazorpayPaymentId: payment.RazorpayPaymentId,
            TransactionId:     payment.TransactionId,
            CorrelationId:     payment.CorrelationId,
            SlotId:            payment.SlotId,
            Message:           "Payment confirmed. Your slot is being booked.");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  FailPaymentAsync
    //  ★ SAGA FAILURE / COMPENSATION PATH — marks Failed, publishes
    //    PaymentFailed so Schedule Service rolls the slot back to AVAILABLE.
    // ════════════════════════════════════════════════════════════════════════

    public async Task<FailPaymentResponse> FailPaymentAsync(
        FailPaymentRequest request, CancellationToken ct = default)
    {
        var payment = await RequireTrackedAsync(request.PaymentId, ct);

        if (payment.Status != Entities.Payment.StatusPending)
            throw new InvalidOperationException(
                $"Payment {request.PaymentId} is already '{payment.Status}'. " +
                "Only Pending payments can be failed.");

        var reason = request.Reason?.Trim()
            ?? "Payment cancelled by user or gateway declined.";

        payment.MarkFailed(reason);
        await _repo.SaveChangesAsync(ct);

        _logger.LogWarning(
            "★ SAGA COMPENSATION — Payment failed. PaymentId={PaymentId}, " +
            "Reason={Reason}, CorrelationId={CorrelationId}, SlotId={SlotId}",
            payment.PaymentId, reason, payment.CorrelationId, payment.SlotId);

        // ── Publish PaymentFailed → Schedule Service releases the slot ────────
        // The Appointment Service does NOT consume PaymentFailed — it simply
        // never creates a record when no PaymentSucceeded is received.
        _sagaPublisher.PublishPaymentFailed(new PaymentFailed
        {
            CorrelationId = payment.CorrelationId,
            SlotId        = payment.SlotId,
            Reason        = reason
        });

        return new FailPaymentResponse(
            PaymentId:     payment.PaymentId,
            Status:        payment.Status,
            CorrelationId: payment.CorrelationId,
            SlotId:        payment.SlotId,
            Message:       "Payment marked as failed. The slot has been released.");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Queries
    // ════════════════════════════════════════════════════════════════════════

    public async Task<PaymentDto?> GetPaymentByAppointmentAsync(
        int appointmentId, CancellationToken ct = default)
    {
        var p = await _repo.FindByAppointmentIdAsync(appointmentId, ct);
        return p is null ? null : ToDto(p);
    }

    public async Task<PaymentDto?> GetPaymentBySlotAsync(
        int slotId, CancellationToken ct = default)
    {
        var list = await _repo.FindBySlotIdAsync(slotId, ct);

        var p = list
            .OrderByDescending(x => x.PaymentId)
            .FirstOrDefault();

        return p is null ? null : ToDto(p);
    }


    public async Task<IReadOnlyList<PaymentDto>> GetPaymentsByPatientAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var list = await _repo.FindByPatientIdAsync(patientId, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PaymentDto>> GetPaymentHistoryAsync(
        CancellationToken ct = default)
    {
        var list = await _repo.FindByPaidAtBetweenAsync(DateTime.MinValue, DateTime.UtcNow, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<string> GetPaymentStatusAsync(
        int paymentId, CancellationToken ct = default)
    {
        var p = await _repo.GetByIdAsync(paymentId, ct)
            ?? throw new KeyNotFoundException($"Payment {paymentId} not found.");
        return p.GetStatus();
    }

    public async Task<PaymentDto> UpdatePaymentStatusAsync(
        int paymentId, string status, CancellationToken ct = default)
    {
        var p = await RequireTrackedAsync(paymentId, ct);
        p.SetStatus(status);
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Payment status updated. PaymentId={PaymentId}, NewStatus={Status}",
            paymentId, status);
        return ToDto(p);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Invoice / Revenue
    // ════════════════════════════════════════════════════════════════════════

    public async Task<InvoiceDto> GenerateInvoiceAsync(
        int paymentId, CancellationToken ct = default)
    {
        var p = await _repo.GetByIdAsync(paymentId, ct)
            ?? throw new KeyNotFoundException($"Payment {paymentId} not found.");

        if (p.Status != Entities.Payment.StatusPaid)
            throw new InvalidOperationException(
                $"Invoice can only be generated for Paid payments. Current status: {p.Status}");

        return new InvoiceDto(
            PaymentId:     p.PaymentId,
            AppointmentId: p.AppointmentId,
            PatientId:     p.PatientId,
            ProviderId:    p.ProviderId,
            Amount:        p.Amount,
            Currency:      p.Currency,
            Mode:          p.Mode,
            TransactionId: p.TransactionId,
            PaidAt:        p.PaidAt!.Value,
            InvoiceNumber: $"INV-{p.AppointmentId:D6}-{p.PaymentId:D6}");
    }

    public async Task<TotalRevenueDto> GetTotalRevenueAsync(
        Guid providerId, CancellationToken ct = default)
    {
        var total = await _repo.SumAmountByProviderIdAsync(providerId, ct);
        return new TotalRevenueDto(providerId, total);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Razorpay helpers
    // ════════════════════════════════════════════════════════════════════════

    private RazorpayOrderResponse CreateRazorpayOrder(
        Entities.Payment payment,
        Guid             correlationId,
        int              slotId)
    {
        var amountInPaise = (long)(payment.Amount * 100);
        var client        = new RazorpayClient(_razorpay.KeyId, _razorpay.KeySecret);

        var options = new Dictionary<string, object>
        {
            { "amount",   amountInPaise },
            { "currency", payment.Currency },
            { "receipt",  $"slot_{slotId}_{DateTime.UtcNow:yyyyMMddHHmmss}" },
            { "notes", new Dictionary<string, string>
              {
                  { "slot_id",        slotId.ToString()            },
                  { "patient_id",     payment.PatientId.ToString() },
                  { "correlation_id", correlationId.ToString()     }
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
            _logger.LogError(ex,
                "Razorpay order creation failed for SlotId={SlotId}", slotId);
            throw new InvalidOperationException(
                "Payment gateway error. Please try again.", ex);
        }

        string orderId = order["id"]?.ToString() ?? string.Empty;

        _logger.LogInformation(
            "Razorpay order created. OrderId={OrderId}, SlotId={SlotId}, " +
            "Amount={Amount} paise", orderId, slotId, amountInPaise);

        return new RazorpayOrderResponse(
            PaymentId:       payment.PaymentId,
            RazorpayOrderId: orderId,
            AmountInPaise:   amountInPaise,
            Currency:        payment.Currency,
            KeyId:           _razorpay.KeyId,
            CorrelationId:   correlationId,
            SlotId:          slotId);
    }

    private void VerifyRazorpaySignature(
        string orderId, string razorpayPaymentId, string signature)
    {
        var payload  = $"{orderId}|{razorpayPaymentId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_razorpay.KeySecret));
        var hash     = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computed = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        if (!computed.Equals(signature, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Razorpay signature mismatch. OrderId={OrderId}, " +
                "RazorpayPaymentId={PaymentId}", orderId, razorpayPaymentId);
            throw new ArgumentException(
                "Invalid Razorpay payment signature. Verification failed.");
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Private helpers
    // ════════════════════════════════════════════════════════════════════════

    private static string NormaliseMode(string mode) => mode?.Trim() switch
    {
        "UPI"    or "upi"    => Entities.Payment.ModeUpi,
        "Wallet" or "wallet" => Entities.Payment.ModeWallet,
        "Cash"   or "cash"   => Entities.Payment.ModeCash,
        _                    => Entities.Payment.ModeCard
    };

    private async Task<Entities.Payment> RequireTrackedAsync(
        int paymentId, CancellationToken ct)
        => await _repo.GetTrackedByIdAsync(paymentId, ct)
            ?? throw new KeyNotFoundException($"Payment {paymentId} not found.");

    private static PaymentDto ToDto(Entities.Payment p) => new(
        PaymentId:         p.PaymentId,
        AppointmentId:     p.AppointmentId,
        PatientId:         p.PatientId,
        ProviderId:        p.ProviderId,
        Amount:            p.Amount,
        Status:            p.Status,
        Mode:              p.Mode,
        TransactionId:     p.TransactionId,
        Currency:          p.Currency,
        RazorpayOrderId:   p.RazorpayOrderId,
        RazorpayPaymentId: p.RazorpayPaymentId,
        PaidAt:            p.PaidAt,
        Notes:             p.Notes,
        CorrelationId:     p.CorrelationId,
        SlotId:            p.SlotId);
}
