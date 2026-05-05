namespace MediBook.Payment.API.Entities;

/// <summary>
/// Payment aggregate linked to exactly one appointment / slot.
///
/// Status lifecycle:
///   Pending → Paid      (user completes Razorpay checkout, signature verified)
///   Pending → Failed    (user cancels checkout or /payments/fail is called)
///   Paid    → Refunded  (on appointment cancellation)
///
/// Saga coordination fields:
///   CorrelationId  — ties this payment to a specific Saga instance
///   SlotId         — the slot being held during the Saga
///
/// Appointment pass-through fields:
///   These are sourced from the PaymentRequested event and stored here so
///   that POST /payments/confirm can publish a complete PaymentSucceeded
///   event without making any synchronous call to other services.
/// </summary>
public sealed class Payment
{
    // ── Status constants ─────────────────────────────────────────────────────
    public const string StatusPending  = "Pending";
    public const string StatusPaid     = "Paid";
    public const string StatusFailed   = "Failed";
    public const string StatusRefunded = "Refunded";

    public static readonly IReadOnlySet<string> ValidStatuses =
        new HashSet<string> { StatusPending, StatusPaid, StatusFailed, StatusRefunded };

    // ── Mode constants ───────────────────────────────────────────────────────
    public const string ModeCard   = "Card";
    public const string ModeUpi    = "UPI";
    public const string ModeWallet = "Wallet";
    public const string ModeCash   = "Cash";

    public static readonly IReadOnlySet<string> ValidModes =
        new HashSet<string> { ModeCard, ModeUpi, ModeWallet, ModeCash };

    // ── Properties ────────────────────────────────────────────────────────────
    public int       PaymentId         { get; private set; }
    public int       AppointmentId     { get; private set; }
    public Guid      PatientId         { get; private set; }
    public Guid      ProviderId        { get; private set; }
    public decimal   Amount            { get; private set; }
    public string    Status            { get; private set; } = StatusPending;
    public string    Mode              { get; private set; } = string.Empty;
    public string    TransactionId     { get; private set; } = string.Empty;
    public string    Currency          { get; private set; } = "INR";
    public string    RazorpayOrderId   { get; private set; } = string.Empty;
    public string    RazorpayPaymentId { get; private set; } = string.Empty;
    public DateTime? PaidAt            { get; private set; }
    public string    Notes             { get; private set; } = string.Empty;

    // ── Saga coordination fields ──────────────────────────────────────────────

    /// <summary>
    /// Ties this payment to a specific Saga instance originated in the Schedule Service.
    /// Required by POST /payments/confirm and POST /payments/fail so they can publish
    /// outcome events with the correct CorrelationId without calling back to Schedule Service.
    /// </summary>
    public Guid CorrelationId { get; private set; }

    /// <summary>
    /// The slot ID being held PENDING during this Saga.
    /// Embedded here so confirm/fail endpoints include SlotId in published Saga events.
    /// </summary>
    public int SlotId { get; private set; }

    // ── Appointment pass-through fields ───────────────────────────────────────
    // Sourced from PaymentRequested; echoed in PaymentSucceeded so the
    // Appointment Service can create a complete record with no HTTP calls.

    public string AppointmentDate    { get; private set; } = string.Empty;
    public string StartTime          { get; private set; } = string.Empty;
    public string EndTime            { get; private set; } = string.Empty;
    public string ServiceType        { get; private set; } = string.Empty;
    public string ModeOfConsultation { get; private set; } = string.Empty;

    private Payment() { } // EF Core

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new PENDING payment.
    /// <paramref name="correlationId"/> and <paramref name="slotId"/> are required
    /// for Saga coordination — they must originate from the PaymentRequested event.
    /// Appointment pass-through fields should also be supplied from the event.
    /// </summary>
    public static Payment Create(
        int     appointmentId,
        Guid    patientId,
        Guid    providerId,
        decimal amount,
        string  mode,
        Guid    correlationId,
        int     slotId,
        string  currency           = "INR",
        string? notes              = null,
        string  appointmentDate    = "",
        string  startTime          = "",
        string  endTime            = "",
        string  serviceType        = "",
        string  modeOfConsultation = "")
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");
        if (!ValidModes.Contains(mode))
            throw new ArgumentException(
                $"'{mode}' is not a valid payment mode. Valid: {string.Join(", ", ValidModes)}");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.");
        if (correlationId == Guid.Empty)
            throw new ArgumentException("CorrelationId is required for Saga tracking.");

        return new Payment
        {
            AppointmentId     = appointmentId,
            PatientId         = patientId,
            ProviderId        = providerId,
            Amount            = amount,
            Mode              = mode,
            Currency          = currency.Trim().ToUpperInvariant(),
            Status            = StatusPending,
            Notes             = notes?.Trim() ?? string.Empty,
            CorrelationId     = correlationId,
            SlotId            = slotId,
            AppointmentDate   = appointmentDate,
            StartTime         = startTime,
            EndTime           = endTime,
            ServiceType       = serviceType,
            ModeOfConsultation = modeOfConsultation
        };
    }

    // ── State transitions ─────────────────────────────────────────────────────

    /// <summary>Records a successful Razorpay capture.</summary>
    public void MarkPaid(string razorpayOrderId, string razorpayPaymentId, string transactionId)
    {
        if (Status != StatusPending)
            throw new InvalidOperationException(
                $"Cannot mark payment as Paid from status '{Status}'. Only Pending payments can be captured.");
        if (string.IsNullOrWhiteSpace(razorpayPaymentId))
            throw new ArgumentException("RazorpayPaymentId is required.");

        RazorpayOrderId   = razorpayOrderId?.Trim()   ?? string.Empty;
        RazorpayPaymentId = razorpayPaymentId.Trim();
        TransactionId     = transactionId?.Trim()     ?? string.Empty;
        Status            = StatusPaid;
        PaidAt            = DateTime.UtcNow;
    }

    /// <summary>Records a gateway or processing failure.</summary>
    public void MarkFailed(string? reason = null)
    {
        if (Status != StatusPending)
            throw new InvalidOperationException(
                $"Cannot mark payment as Failed from status '{Status}'.");

        Status = StatusFailed;
        if (!string.IsNullOrWhiteSpace(reason))
            Notes = reason.Trim();
    }

    /// <summary>
    /// Marks a Paid payment as Refunded
    /// (status only — actual gateway refund is caller's responsibility).
    /// </summary>
    public void MarkRefunded()
    {
        if (Status != StatusPaid)
            throw new InvalidOperationException(
                $"Cannot refund a payment with status '{Status}'. Only Paid payments can be refunded.");

        Status = StatusRefunded;
    }

    /// <summary>Generic status update — used by admin/internal calls.</summary>
    public void SetStatus(string status)
    {
        if (!ValidStatuses.Contains(status))
            throw new ArgumentException(
                $"'{status}' is not a valid payment status. Valid: {string.Join(", ", ValidStatuses)}");
        Status = status;
    }

    /// <summary>
    /// Stores the Razorpay orderId on a PENDING payment before the user has acted.
    /// Called by the consumer immediately after order creation — Status remains Pending.
    /// This is intentionally separate from MarkPaid which also sets Status = Paid.
    /// </summary>
    public void SetPendingRazorpayOrderId(string razorpayOrderId)
    {
        if (Status != StatusPending)
            throw new InvalidOperationException(
                $"SetPendingRazorpayOrderId can only be called on Pending payments.");
        RazorpayOrderId = razorpayOrderId?.Trim() ?? string.Empty;
    }

    // ── Read helpers ──────────────────────────────────────────────────────────
    public int    GetPaymentId() => PaymentId;
    public string GetStatus()   => Status;
}
