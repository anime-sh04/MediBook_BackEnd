namespace MediBook.Schedule.API.Messaging.Contracts;

// ═══════════════════════════════════════════════════════════════════════════
//  SAGA EVENT CONTRACTS — Schedule Service (Orchestrator side)
//
//  Flow:
//    1. Schedule Service publishes  → PaymentRequested
//    2. Payment Service publishes   → PaymentSucceeded | PaymentFailed
//    3. Schedule Service consumes   → PaymentSucceeded  (mark slot BOOKED)
//                                   → PaymentFailed     (rollback to AVAILABLE)
//    4. Appointment Service consumes → PaymentSucceeded  (create appointment record)
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Published by the Schedule Service (Saga Orchestrator) when a patient
/// requests to book a slot.  This is the event that STARTS the Saga.
///
/// Carries all appointment-related data so the Appointment Service can
/// create a complete record without calling back to any other service.
///
/// Exchange : medibook.saga  (topic)
/// Routing  : payment.requested
/// </summary>
public sealed record PaymentRequested
{
    /// <summary>Unique correlation ID for this Saga instance.</summary>
    public Guid    CorrelationId      { get; init; } = Guid.NewGuid();

    /// <summary>The slot being booked (held as PENDING until payment confirms).</summary>
    public int     SlotId             { get; init; }

    /// <summary>Patient who is booking.</summary>
    public Guid    PatientId          { get; init; }

    /// <summary>Provider who owns the slot.</summary>
    public Guid    ProviderId         { get; init; }

    /// <summary>Amount to charge (in INR by default).</summary>
    public decimal Amount             { get; init; }

    /// <summary>Payment mode: Card | UPI | Wallet | Cash.</summary>
    public string  Mode               { get; init; } = "Card";

    /// <summary>Currency code, e.g. "INR".</summary>
    public string  Currency           { get; init; } = "INR";

    /// <summary>Optional notes passed to both the payment and appointment records.</summary>
    public string? Notes              { get; init; }

    // ── Appointment booking details (sourced from the Slot entity) ────────────
    // Carried through the Saga so the Appointment Service can create a complete
    // record on PaymentSucceeded without making any synchronous HTTP calls.

    /// <summary>Appointment date (yyyy-MM-dd string for serialisation safety).</summary>
    public string  AppointmentDate    { get; init; } = string.Empty;

    /// <summary>Slot start time (HH:mm).</summary>
    public string  StartTime          { get; init; } = string.Empty;

    /// <summary>Slot end time (HH:mm).</summary>
    public string  EndTime            { get; init; } = string.Empty;

    /// <summary>Type of medical service (e.g. "General Consultation").</summary>
    public string  ServiceType        { get; init; } = string.Empty;

    /// <summary>"In-Person" or "Teleconsultation".</summary>
    public string  ModeOfConsultation { get; init; } = string.Empty;

    /// <summary>UTC timestamp when this event was raised.</summary>
    public DateTime OccurredAt        { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published by the Payment Service when the payment succeeds.
/// Consumed by:
///   • Schedule Service     → marks slot BOOKED (IsBooked = true)
///   • Appointment Service  → creates the appointment record
///
/// Exchange : medibook.saga  (topic)
/// Routing  : payment.succeeded
/// </summary>
public sealed record PaymentSucceeded
{
    /// <summary>Matches the CorrelationId from PaymentRequested.</summary>
    public Guid    CorrelationId      { get; init; }

    /// <summary>The slot that should now be marked CONFIRMED (IsBooked = true).</summary>
    public int     SlotId             { get; init; }

    /// <summary>The payment record created in the Payment Service.</summary>
    public int     PaymentId          { get; init; }

    // ── Appointment creation data (passed through from PaymentRequested) ──────

    public Guid    PatientId          { get; init; }
    public Guid    ProviderId         { get; init; }
    public string  AppointmentDate    { get; init; } = string.Empty;
    public string  StartTime          { get; init; } = string.Empty;
    public string  EndTime            { get; init; } = string.Empty;
    public string  ServiceType        { get; init; } = string.Empty;
    public string  ModeOfConsultation { get; init; } = string.Empty;
    public string? Notes              { get; init; }

    public DateTime OccurredAt        { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published by the Payment Service when payment processing fails.
/// Consumed by the Schedule Service to roll back the slot to AVAILABLE.
/// The Appointment Service does NOT consume this event — no appointment
/// record is ever created when payment fails.
///
/// Exchange : medibook.saga  (topic)
/// Routing  : payment.failed
/// </summary>
public sealed record PaymentFailed
{
    /// <summary>Matches the CorrelationId from PaymentRequested.</summary>
    public Guid   CorrelationId { get; init; }

    /// <summary>The slot that must be rolled back to AVAILABLE.</summary>
    public int    SlotId        { get; init; }

    /// <summary>Human-readable failure reason for logging / debugging.</summary>
    public string Reason        { get; init; } = string.Empty;

    public DateTime OccurredAt  { get; init; } = DateTime.UtcNow;
}
