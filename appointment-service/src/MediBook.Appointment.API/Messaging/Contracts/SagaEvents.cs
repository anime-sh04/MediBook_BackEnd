namespace MediBook.Appointment.API.Messaging.Contracts;

// ═══════════════════════════════════════════════════════════════════════════
//  SAGA EVENT CONTRACTS — Appointment Service (Participant side)
//
//  The Appointment Service consumes:
//    PaymentSucceeded  →  create appointment record
//
//  It does NOT publish any Saga events.
//  It does NOT consume PaymentFailed — a failed payment means no appointment
//  record should be created, which is simply the absence of action.
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Consumed by the Appointment Service after the Payment Service confirms
/// a successful payment.  Carries all data needed to create the appointment
/// record without making any HTTP call to Schedule or Payment services.
///
/// Exchange : medibook.saga  (topic)
/// Routing  : payment.succeeded
/// </summary>
public sealed record PaymentSucceeded
{
    /// <summary>Matches the CorrelationId from PaymentRequested.</summary>
    public Guid    CorrelationId      { get; init; }

    /// <summary>The slot that has been confirmed as BOOKED.</summary>
    public int     SlotId             { get; init; }

    /// <summary>The payment record created in the Payment Service.</summary>
    public int     PaymentId          { get; init; }

    // ── Appointment creation data ─────────────────────────────────────────────
    // Passed through from PaymentRequested → Payment entity → here.
    // All fields needed by Appointment.Create() are present so no HTTP call
    // to Schedule or Payment services is required.

    public Guid    PatientId          { get; init; }
    public Guid    ProviderId         { get; init; }
    public string  AppointmentDate    { get; init; } = string.Empty;  // "yyyy-MM-dd"
    public string  StartTime          { get; init; } = string.Empty;  // "HH:mm"
    public string  EndTime            { get; init; } = string.Empty;  // "HH:mm"
    public string  ServiceType        { get; init; } = string.Empty;
    public string  ModeOfConsultation { get; init; } = string.Empty;
    public string? Notes              { get; init; }

    public DateTime OccurredAt        { get; init; } = DateTime.UtcNow;
}
