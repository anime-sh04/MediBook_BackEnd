using MediBook.Payment.API.DTOs;

namespace MediBook.Payment.API.Services;

/// <summary>
/// Business-logic contract for the Payment Service.
///
/// Saga-aware design:
///   ProcessPaymentAsync  — creates Razorpay order + PENDING record (no auto-completion)
///   ConfirmPaymentAsync  — verifies signature, marks Paid, publishes PaymentSucceeded
///   FailPaymentAsync     — marks Failed, publishes PaymentFailed (compensation)
/// </summary>
public interface IPaymentService
{
    // ── Saga payment flow ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Razorpay order for online modes (Card/UPI/Wallet) or a Pending
    /// Cash record, and persists the payment with Status = Pending.
    /// Does NOT process or complete the payment — the user does that via checkout.
    /// </summary>
    Task<(PaymentDto Payment, RazorpayOrderResponse? RazorpayOrder)> ProcessPaymentAsync(
        ProcessPaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// ★ SAGA SUCCESS PATH
    /// Verifies the Razorpay signature, marks the payment as Paid, and
    /// publishes PaymentSucceeded so the Schedule Service confirms the slot.
    /// Throws <see cref="ArgumentException"/> if signature verification fails.
    /// </summary>
    Task<ConfirmPaymentResponse> ConfirmPaymentAsync(
        ConfirmPaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// ★ SAGA FAILURE / COMPENSATION PATH
    /// Marks the payment as Failed and publishes PaymentFailed so the
    /// Schedule Service rolls the slot back to AVAILABLE.
    /// Called when the user cancels the Razorpay checkout or the gateway errors.
    /// </summary>
    Task<FailPaymentResponse> FailPaymentAsync(
        FailPaymentRequest request, CancellationToken ct = default);

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Returns the payment linked to the given appointment / slot.</summary>
    Task<PaymentDto?> GetPaymentByAppointmentAsync(int appointmentId, CancellationToken ct = default);

    /// <summary>Returns the payment associated with a specific slot (SlotId == AppointmentId).</summary>
    Task<PaymentDto?> GetPaymentBySlotAsync(int slotId, CancellationToken ct = default);

    /// <summary>Returns all payments made by a patient.</summary>
    Task<IReadOnlyList<PaymentDto>> GetPaymentsByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>Returns all payments (across all patients). Admin use.</summary>
    Task<IReadOnlyList<PaymentDto>> GetPaymentHistoryAsync(CancellationToken ct = default);

    /// <summary>Returns the current status string for a payment.</summary>
    Task<string> GetPaymentStatusAsync(int paymentId, CancellationToken ct = default);

    /// <summary>Generic status update (admin/internal override).</summary>
    Task<PaymentDto> UpdatePaymentStatusAsync(int paymentId, string status, CancellationToken ct = default);

    /// <summary>Generates an invoice DTO for a completed (Paid) payment.</summary>
    Task<InvoiceDto> GenerateInvoiceAsync(int paymentId, CancellationToken ct = default);

    /// <summary>Returns the total paid revenue for a provider.</summary>
    Task<TotalRevenueDto> GetTotalRevenueAsync(Guid providerId, CancellationToken ct = default);
}
