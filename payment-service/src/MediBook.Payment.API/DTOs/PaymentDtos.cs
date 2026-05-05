namespace MediBook.Payment.API.DTOs;

// ════════════════════════════════════════════════════════════════════════════
//  REQUEST DTOs
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// POST /payments/process
/// Initiates a payment for a slot booking:
///   - Online (Card/UPI/Wallet): creates a Razorpay order, returns orderId to frontend.
///   - Cash: records a Pending entry; confirm immediately if needed.
///
/// This endpoint is typically called by the frontend after receiving the
/// 202 Accepted from PUT /slots/{id}/book (which publishes the PaymentRequested
/// event).  The consumer stores the CorrelationId/SlotId; the frontend uses
/// this endpoint to get the Razorpay orderId needed to open the checkout widget.
/// </summary>
public sealed record ProcessPaymentRequest(
    int     AppointmentId,
    Guid    PatientId,
    Guid    ProviderId,
    decimal Amount,
    string  Mode,           // Card | UPI | Wallet | Cash
    Guid    CorrelationId,  // From the Saga — returned by PUT /slots/{id}/book
    int     SlotId,         // The slot being booked
    string  Currency = "INR",
    string? Notes    = null
);

/// <summary>
/// POST /payments/confirm
/// Called by the frontend after the user successfully completes the
/// Razorpay checkout widget.
///
/// Saga action:
///   1. Verifies the Razorpay signature (HMAC-SHA256).
///   2. Marks payment Status = Paid.
///   3. Publishes PaymentSucceeded → Schedule Service confirms the slot.
/// </summary>
public sealed record ConfirmPaymentRequest(
    int    PaymentId,
    string RazorpayOrderId,
    string RazorpayPaymentId,
    string RazorpaySignature,
    string TransactionId
);

/// <summary>
/// POST /payments/fail
/// Called by the frontend when the user cancels the Razorpay checkout
/// or when the gateway reports a failure.
///
/// Saga action:
///   1. Marks payment Status = Failed.
///   2. Publishes PaymentFailed → Schedule Service rolls the slot back to AVAILABLE.
/// </summary>
public sealed record FailPaymentRequest(
    int     PaymentId,
    string? Reason = null   // Optional failure description for auditing
);

// ════════════════════════════════════════════════════════════════════════════
//  RESPONSE DTOs
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Full payment details — returned from all read/mutate endpoints.</summary>
public sealed record PaymentDto(
    int       PaymentId,
    int       AppointmentId,
    Guid      PatientId,
    Guid      ProviderId,
    decimal   Amount,
    string    Status,
    string    Mode,
    string    TransactionId,
    string    Currency,
    string    RazorpayOrderId,
    string    RazorpayPaymentId,
    DateTime? PaidAt,
    string    Notes,
    Guid      CorrelationId,  // Saga correlation — useful for frontend tracking
    int       SlotId          // The slot involved in this payment
);

/// <summary>
/// Returned by POST /payments/process for online payment modes.
/// The frontend uses RazorpayOrderId and KeyId to initialise the Razorpay
/// checkout widget.
/// </summary>
public sealed record RazorpayOrderResponse(
    int     PaymentId,
    string  RazorpayOrderId,
    decimal AmountInPaise,   // Razorpay expects amount in smallest currency unit (paise)
    string  Currency,
    string  KeyId,           // Public Razorpay key for the frontend SDK
    Guid    CorrelationId,   // Saga correlation — echo back for frontend convenience
    int     SlotId
);

/// <summary>
/// Returned by POST /payments/confirm after successful signature verification.
/// The frontend can use this to show a booking-confirmed screen.
/// </summary>
public sealed record ConfirmPaymentResponse(
    int     PaymentId,
    string  Status,          // Always "Paid" on success
    string  RazorpayOrderId,
    string  RazorpayPaymentId,
    string  TransactionId,
    Guid    CorrelationId,
    int     SlotId,
    string  Message          // Human-readable confirmation message
);

/// <summary>
/// Returned by POST /payments/fail.
/// </summary>
public sealed record FailPaymentResponse(
    int    PaymentId,
    string Status,           // Always "Failed"
    Guid   CorrelationId,
    int    SlotId,
    string Message
);

public sealed record InvoiceDto(
    int      PaymentId,
    int      AppointmentId,
    Guid     PatientId,
    Guid     ProviderId,
    decimal  Amount,
    string   Currency,
    string   Mode,
    string   TransactionId,
    DateTime PaidAt,
    string   InvoiceNumber
);

public sealed record TotalRevenueDto(Guid ProviderId, decimal TotalRevenue);

// ── Shared ────────────────────────────────────────────────────────────────────
public sealed record ApiErrorResponse(
    string               Message,
    IEnumerable<string>? Errors = null
);
