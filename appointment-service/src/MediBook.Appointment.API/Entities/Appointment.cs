namespace MediBook.Appointment.API.Entities;

/// <summary>
/// Core booking aggregate.
/// Status lifecycle:
///   Scheduled → Completed
///   Scheduled → Cancelled
///   Scheduled → No-Show
///   Cancelled  (terminal)
///   Completed  (terminal)
///   No-Show    (terminal)
/// </summary>
public sealed class Appointment
{
    // ── Valid status constants ────────────────────────────────────────────────
    public const string StatusScheduled = "Scheduled";
    public const string StatusCompleted = "Completed";
    public const string StatusCancelled = "Cancelled";
    public const string StatusNoShow    = "No-Show";

    public static readonly IReadOnlySet<string> ValidStatuses =
        new HashSet<string> { StatusScheduled, StatusCompleted, StatusCancelled, StatusNoShow };

    // ── Properties ────────────────────────────────────────────────────────────
    public int      AppointmentId      { get; private set; }
    public Guid     PatientId          { get; private set; }
    public Guid     ProviderId         { get; private set; }
    public int      SlotId             { get; private set; }
    public string   ServiceType        { get; private set; } = string.Empty;
    public DateOnly AppointmentDate    { get; private set; }
    public TimeOnly StartTime          { get; private set; }
    public TimeOnly EndTime            { get; private set; }
    public string   Status             { get; private set; } = StatusScheduled;
    public string   Notes              { get; private set; } = string.Empty;
    public string   ModeOfConsultation { get; private set; } = string.Empty;
    public DateTime CreatedAt          { get; private set; }
    public DateTime UpdatedAt          { get; private set; }

    private Appointment() { } // EF Core

    // ── Factory ───────────────────────────────────────────────────────────────

    public static Appointment Create(
        Guid     patientId,
        Guid     providerId,
        int      slotId,
        string   serviceType,
        DateOnly appointmentDate,
        TimeOnly startTime,
        TimeOnly endTime,
        string   modeOfConsultation,
        string?  notes = null)
    {
        if (string.IsNullOrWhiteSpace(serviceType))
            throw new ArgumentException("ServiceType is required.");
        if (string.IsNullOrWhiteSpace(modeOfConsultation))
            throw new ArgumentException("ModeOfConsultation is required.");
        if (endTime <= startTime)
            throw new ArgumentException("EndTime must be after StartTime.");

        var now = DateTime.UtcNow;
        return new Appointment
        {
            PatientId          = patientId,
            ProviderId         = providerId,
            SlotId             = slotId,
            ServiceType        = serviceType.Trim(),
            AppointmentDate    = appointmentDate,
            StartTime          = startTime,
            EndTime            = endTime,
            Status             = StatusScheduled,
            Notes              = notes?.Trim() ?? string.Empty,
            ModeOfConsultation = modeOfConsultation.Trim(),
            CreatedAt          = now,
            UpdatedAt          = now
        };
    }

    // ── State transitions ─────────────────────────────────────────────────────

    /// <summary>Cancels a scheduled appointment.</summary>
    public void Cancel()
    {
        EnsureScheduled(nameof(Cancel));
        Status    = StatusCancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Marks a scheduled appointment as completed.</summary>
    public void Complete()
    {
        EnsureScheduled(nameof(Complete));
        Status    = StatusCompleted;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Marks a scheduled appointment as a no-show.</summary>
    public void MarkNoShow()
    {
        EnsureScheduled(nameof(MarkNoShow));
        Status    = StatusNoShow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reschedule: updates SlotId, date and time.
    /// Only allowed when Scheduled.
    /// </summary>
    public void Reschedule(
        int      newSlotId,
        DateOnly newDate,
        TimeOnly newStartTime,
        TimeOnly newEndTime)
    {
        EnsureScheduled(nameof(Reschedule));
        if (newEndTime <= newStartTime)
            throw new ArgumentException("EndTime must be after StartTime.");

        SlotId          = newSlotId;
        AppointmentDate = newDate;
        StartTime       = newStartTime;
        EndTime         = newEndTime;
        UpdatedAt       = DateTime.UtcNow;
    }

    /// <summary>
    /// Generic status update — used by admin/internal calls.
    /// Validates the target status is a known value.
    /// </summary>
    public void SetStatus(string status)
    {
        if (!ValidStatuses.Contains(status))
            throw new ArgumentException($"'{status}' is not a valid appointment status. " +
                $"Valid values: {string.Join(", ", ValidStatuses)}");
        Status    = status;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Update clinical notes.</summary>
    public void UpdateNotes(string notes)
    {
        Notes     = notes?.Trim() ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Read helpers (match class diagram) ────────────────────────────────────
    public int    GetAppointmentId() => AppointmentId;
    public string GetStatus()        => Status;

    // ── Private guard ─────────────────────────────────────────────────────────
    private void EnsureScheduled(string operation)
    {
        if (Status != StatusScheduled)
            throw new InvalidOperationException(
                $"Cannot {operation} an appointment with status '{Status}'. " +
                "Only Scheduled appointments can be transitioned.");
    }
}
