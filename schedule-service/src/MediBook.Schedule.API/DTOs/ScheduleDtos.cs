namespace MediBook.Schedule.API.DTOs;

// ── Slot management ──────────────────────────────────────────────────────────

public sealed record AddSlotRequest(
    Guid   ProviderId,
    string Date,
    string StartTime,
    string EndTime,
    decimal Price,              
    string Recurrence = "none"
);

public sealed record AddBulkSlotsRequest(IReadOnlyList<AddSlotRequest> Slots);

public sealed record UpdateSlotRequest(
    string Date,
    string StartTime,
    string EndTime,
    string Recurrence = "none"
);

public sealed record GenerateRecurringRequest(
    Guid   ProviderId,
    string StartDate,
    string EndDate,
    string SlotStartTime,
    string SlotEndTime,
    string Recurrence,
    decimal Price
);

// ── Saga booking request ─────────────────────────────────────────────────────

/// <summary>
/// Request body for PUT /api/v1/slots/{id}/book — the Saga entry point.
///
/// Carries payment details AND appointment booking details so the full
/// context can be embedded in the PaymentRequested event.  This allows
/// the Appointment Service to create a complete record on PaymentSucceeded
/// without any synchronous cross-service HTTP calls.
/// </summary>
public sealed record BookSlotRequest(
    Guid    PatientId,
    Guid    ProviderId,

    // Payment details
    string  Mode               = "Card",
    string  Currency           = "INR",
    string? Notes              = null,

    // Appointment booking details
    string  ServiceType        = "General Consultation",
    string  ModeOfConsultation = "In-Person"
);

/// <summary>
/// Response returned when a booking is initiated via the Saga.
/// The slot is PENDING until the payment outcome is received.
/// </summary>
public sealed record BookSlotResponse(
    int    SlotId,
    Guid   CorrelationId,
    string Status  // "PENDING"
);

// ── Response DTOs ────────────────────────────────────────────────────────────

public sealed record AvailabilitySlotDto(
    int      SlotId,
    Guid     ProviderId,
    string   Date,
    string   StartTime,
    string   EndTime,
    int      DurationMinutes,
    bool     IsBooked,
    bool     IsBlocked,
    string   Recurrence,
    DateTime CreatedAt
);

// ── Shared ────────────────────────────────────────────────────────────────────

public sealed record ApiErrorResponse(
    string               Message,
    IEnumerable<string>? Errors = null
);
