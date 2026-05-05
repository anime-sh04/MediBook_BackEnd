namespace MediBook.Appointment.API.DTOs;

// ── Saga-driven creation command ─────────────────────────────────────────────

/// <summary>
/// Internal command issued by <see cref="MediBook.Appointment.API.Messaging.Consumers.PaymentSucceededConsumer"/>
/// to create an appointment record after a successful payment.
/// All fields originate from the PaymentSucceeded event.
/// </summary>
public sealed record CreateAppointmentFromSagaCommand(
    Guid    PatientId,
    Guid    ProviderId,
    int     SlotId,
    string  ServiceType,
    string  AppointmentDate,     // "yyyy-MM-dd"
    string  StartTime,           // "HH:mm"
    string  EndTime,             // "HH:mm"
    string  ModeOfConsultation,
    Guid    CorrelationId,
    string? Notes = null
);

// ── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>Payload to reschedule an existing appointment to a new slot.</summary>
public sealed record RescheduleRequest(
    int    NewSlotId,
    string NewAppointmentDate, // "yyyy-MM-dd"
    string NewStartTime,       // "HH:mm"
    string NewEndTime          // "HH:mm"
);

/// <summary>Payload for the generic status-update endpoint.</summary>
public sealed record UpdateStatusRequest(string Status);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public sealed record AppointmentDto(
    int      AppointmentId,
    Guid     PatientId,
    Guid     ProviderId,
    int      SlotId,
    string   ServiceType,
    string   AppointmentDate,
    string   StartTime,
    string   EndTime,
    string   Status,
    string   Notes,
    string   ModeOfConsultation,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record AppointmentCountDto(Guid ProviderId, int Count);

// ── Shared ────────────────────────────────────────────────────────────────────

public sealed record ApiErrorResponse(
    string               Message,
    IEnumerable<string>? Errors = null
);

// ── Schedule-service HTTP client DTOs ────────────────────────────────────────

public sealed record SlotDto(
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
