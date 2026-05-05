using MediBook.Appointment.API.DTOs;
using MediBook.Appointment.API.Repositories;

namespace MediBook.Appointment.API.Services;

public sealed class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository          _repo;
    private readonly IScheduleClient                 _schedSvc;
    private readonly IPaymentClient                  _paySvc;
    private readonly ILogger<AppointmentService>     _logger;

    public AppointmentService(
        IAppointmentRepository      repo,
        IScheduleClient             schedSvc,
        IPaymentClient              paySvc,
        ILogger<AppointmentService> logger)
    {
        _repo     = repo;
        _schedSvc = schedSvc;
        _paySvc   = paySvc;
        _logger   = logger;
    }

    // ── Saga-driven appointment creation ──────────────────────────────────────

    /// <summary>
    /// Creates an appointment record in response to a PaymentSucceeded event.
    ///
    /// This method is called exclusively by PaymentSucceededConsumer — never by
    /// the HTTP API.  Slot booking is already handled by the Schedule Service's
    /// PaymentResultConsumer; this service only creates the appointment record.
    ///
    /// Idempotent: if an appointment for this SlotId already exists (duplicate
    /// event delivery), the existing record is returned without modification.
    /// </summary>
    public async Task<AppointmentDto> CreateFromSagaAsync(
        CreateAppointmentFromSagaCommand command, CancellationToken ct = default)
    {
        // ── Idempotency: check for existing appointment for this slot ─────────
        var existing = await _repo.FindBySlotIdAsync(command.SlotId, ct);
        if (existing is not null)
        {
            _logger.LogInformation(
                "[Appointment] CreateFromSagaAsync — appointment already exists for " +
                "SlotId={SlotId} (AppointmentId={AppointmentId}). Returning existing record.",
                command.SlotId, existing.AppointmentId);
            return MapToDto(existing);
        }

        // ── Create appointment ────────────────────────────────────────────────
        var appointment = Entities.Appointment.Create(
            command.PatientId,
            command.ProviderId,
            command.SlotId,
            command.ServiceType,
            ParseDate(command.AppointmentDate),
            ParseTime(command.StartTime),
            ParseTime(command.EndTime),
            command.ModeOfConsultation,
            command.Notes);

        var saved = await _repo.AddAsync(appointment, ct);

        _logger.LogInformation(
            "[Appointment] ★ Appointment {AppointmentId} created via Saga. " +
            "Patient={PatientId}, Provider={ProviderId}, Slot={SlotId}, CorrelationId={CorrelationId}",
            saved.AppointmentId, saved.PatientId, saved.ProviderId,
            saved.SlotId, command.CorrelationId);

        return MapToDto(saved);
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    public async Task<AppointmentDto?> GetByIdAsync(
        int appointmentId, CancellationToken ct = default)
    {
        var appt = await _repo.GetByIdAsync(appointmentId, ct)
            ?? throw new KeyNotFoundException($"Appointment {appointmentId} not found.");
        return MapToDto(appt);
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetByPatientAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var appts = await _repo.FindByPatientIdAsync(patientId, ct);
        return appts.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetByProviderAsync(
        Guid providerId, CancellationToken ct = default)
    {
        var appts = await _repo.FindByProviderIdAsync(providerId, ct);
        return appts.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetByProviderAndDateAsync(
        Guid providerId, DateOnly date, CancellationToken ct = default)
    {
        var appts = await _repo.FindByProviderIdAndAppointmentDateAsync(providerId, date, ct);
        return appts.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetUpcomingByPatientAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var appts = await _repo.FindUpcomingByPatientIdAsync(patientId, ct);
        return appts.Select(MapToDto).ToList();
    }

    public Task<int> GetAppointmentCountAsync(
        Guid providerId, CancellationToken ct = default) =>
        _repo.CountByProviderIdAsync(providerId, ct);

    // ── CancelAppointment ─────────────────────────────────────────────────────

    public async Task CancelAppointmentAsync(
        int appointmentId, CancellationToken ct = default)
    {
        var appt = await RequireAppointmentAsync(appointmentId, ct);

        int oldSlotId = appt.SlotId;
        appt.Cancel();
        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation("Appointment {Id} cancelled.", appointmentId);

        // Release the slot — fire-and-forget style so cancellation succeeds
        // even if the Schedule Service is temporarily unavailable.
        try   { await _schedSvc.UnbookSlotAsync(oldSlotId, ct); }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to release slot {SlotId} after cancelling appointment {Id}. " +
                "Manual reconciliation may be required.", oldSlotId, appointmentId);
        }

        // Trigger refund (stubbed)
        try   { await _paySvc.RefundAsync(appointmentId, ct); }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Refund failed for appointment {Id}. Payment service error.", appointmentId);
        }
    }

    // ── RescheduleAppointment ─────────────────────────────────────────────────

    public async Task<AppointmentDto> RescheduleAppointmentAsync(
        int appointmentId, RescheduleRequest request, CancellationToken ct = default)
    {
        var appt = await RequireAppointmentAsync(appointmentId, ct);

        // Validate new slot availability via Schedule Service
        var newSlot = await _schedSvc.GetSlotAsync(request.NewSlotId, ct)
            ?? throw new KeyNotFoundException($"New slot {request.NewSlotId} not found.");

        if (newSlot.IsBooked)
            throw new InvalidOperationException($"New slot {request.NewSlotId} is already booked.");
        if (newSlot.IsBlocked)
            throw new InvalidOperationException($"New slot {request.NewSlotId} is blocked.");

        int oldSlotId = appt.SlotId;

        // Release old slot
        try   { await _schedSvc.UnbookSlotAsync(oldSlotId, ct); }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Could not release old slot {OldSlotId} during reschedule of appointment {Id}.",
                oldSlotId, appointmentId);
        }

        // Update appointment record
        appt.Reschedule(
            request.NewSlotId,
            ParseDate(request.NewAppointmentDate),
            ParseTime(request.NewStartTime),
            ParseTime(request.NewEndTime));

        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Appointment {Id} rescheduled from slot {Old} to slot {New}.",
            appointmentId, oldSlotId, request.NewSlotId);

        return MapToDto(appt);
    }

    // ── CompleteAppointment ───────────────────────────────────────────────────

    public async Task CompleteAppointmentAsync(
        int appointmentId, CancellationToken ct = default)
    {
        var appt = await RequireAppointmentAsync(appointmentId, ct);
        appt.Complete();
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Appointment {Id} completed.", appointmentId);
    }

    // ── UpdateStatus ──────────────────────────────────────────────────────────

    public async Task<string> UpdateStatusAsync(
        int appointmentId, string status, CancellationToken ct = default)
    {
        var appt = await RequireAppointmentAsync(appointmentId, ct);
        appt.SetStatus(status);
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Appointment {Id} status set to {Status}.", appointmentId, status);
        return appt.Status;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Entities.Appointment> RequireAppointmentAsync(
        int appointmentId, CancellationToken ct)
        => await _repo.GetByIdAsync(appointmentId, ct)
           ?? throw new KeyNotFoundException($"Appointment {appointmentId} not found.");

    private static AppointmentDto MapToDto(Entities.Appointment a) => new(
        a.AppointmentId,
        a.PatientId,
        a.ProviderId,
        a.SlotId,
        a.ServiceType,
        a.AppointmentDate.ToString("yyyy-MM-dd"),
        a.StartTime.ToString("HH:mm"),
        a.EndTime.ToString("HH:mm"),
        a.Status,
        a.Notes,
        a.ModeOfConsultation,
        a.CreatedAt,
        a.UpdatedAt);

    private static DateOnly ParseDate(string s)
    {
        if (!DateOnly.TryParse(s, out var d))
            throw new ArgumentException($"Invalid date: '{s}'. Expected yyyy-MM-dd.");
        return d;
    }

    private static TimeOnly ParseTime(string s)
    {
        if (!TimeOnly.TryParse(s, out var t))
            throw new ArgumentException($"Invalid time: '{s}'. Expected HH:mm.");
        return t;
    }
}
