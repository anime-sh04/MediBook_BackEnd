namespace MediBook.Payment.API.Messaging.Contracts;

// ═══════════════════════════════════════════════════════════════════════════
//  SAGA EVENT CONTRACTS — Payment Service (Participant side)
//
//  Duplicated here to keep services independently deployable
//  (no shared class library required).  Must stay in sync with
//  Schedule Service and Appointment Service contracts.
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Consumed by the Payment Service.
/// Published by the Schedule Service (Orchestrator) when a patient requests to book.
/// Exchange : medibook.saga  (topic)
/// Routing  : payment.requested
/// </summary>
public sealed record PaymentRequested
{
    public Guid    CorrelationId      { get; init; }
    public int     SlotId             { get; init; }
    public Guid    PatientId          { get; init; }
    public Guid    ProviderId         { get; init; }
    public decimal Amount             { get; init; }
    public string  Mode               { get; init; } = "Card";
    public string  Currency           { get; init; } = "INR";
    public string? Notes              { get; init; }

    // Appointment details — persisted on the Payment entity and echoed in
    // PaymentSucceeded so the Appointment Service can create a full record.
    public string  AppointmentDate    { get; init; } = string.Empty;
    public string  StartTime          { get; init; } = string.Empty;
    public string  EndTime            { get; init; } = string.Empty;
    public string  ServiceType        { get; init; } = string.Empty;
    public string  ModeOfConsultation { get; init; } = string.Empty;

    public DateTime OccurredAt        { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published by the Payment Service on successful payment confirmation.
/// Consumed by:
///   • Schedule Service     → marks slot BOOKED
///   • Appointment Service  → creates the appointment record
///
/// Exchange : medibook.saga  (topic)
/// Routing  : payment.succeeded
/// </summary>
public sealed record PaymentSucceeded
{
    public Guid    CorrelationId      { get; init; }
    public int     SlotId             { get; init; }
    public int     PaymentId          { get; init; }

    // Appointment creation data — passed through from PaymentRequested
    // so the Appointment Service needs no synchronous HTTP calls.
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
/// Consumed only by the Schedule Service to roll back the slot.
/// Exchange : medibook.saga  (topic)
/// Routing  : payment.failed
/// </summary>
public sealed record PaymentFailed
{
    public Guid    CorrelationId { get; init; }
    public int     SlotId        { get; init; }
    public string  Reason        { get; init; } = string.Empty;
    public DateTime OccurredAt   { get; init; } = DateTime.UtcNow;
}
