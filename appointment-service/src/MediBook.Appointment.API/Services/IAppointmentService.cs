using MediBook.Appointment.API.DTOs;

namespace MediBook.Appointment.API.Services;

/// <summary>
/// Business contract for the Appointment Service.
///
/// Saga design note:
///   BookAppointmentAsync has been REMOVED.  Appointments are no longer
///   created via a direct HTTP POST — they are created reactively by the
///   PaymentSucceededConsumer after a successful payment event.
///
///   CancelAppointmentAsync still calls the Schedule Service to release the
///   slot (PUT /slots/{id}/unbook) because cancellation is a read-model
///   operation that does not go through the payment Saga.
/// </summary>
public interface IAppointmentService
{
    // ── Saga-driven creation (called from PaymentSucceededConsumer) ───────────

    /// <summary>
    /// Creates an appointment record in response to a PaymentSucceeded event.
    /// This is the ONLY way appointments are created in the event-driven flow.
    /// Idempotent — safe to call multiple times for the same SlotId.
    /// </summary>
    Task<AppointmentDto> CreateFromSagaAsync(CreateAppointmentFromSagaCommand command, CancellationToken ct = default);

    // ── Reads ─────────────────────────────────────────────────────────────────

    Task<AppointmentDto?>                  GetByIdAsync(int appointmentId, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentDto>>    GetByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentDto>>    GetByProviderAsync(Guid providerId, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentDto>>    GetByProviderAndDateAsync(Guid providerId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentDto>>    GetUpcomingByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<int>                              GetAppointmentCountAsync(Guid providerId, CancellationToken ct = default);

    // ── Lifecycle transitions ─────────────────────────────────────────────────

    /// <summary>
    /// Cancels a scheduled appointment.
    /// Releases the slot in the Schedule Service and triggers a refund (stubbed).
    /// </summary>
    Task CancelAppointmentAsync(int appointmentId, CancellationToken ct = default);

    /// <summary>
    /// Reschedules to a new slot.
    /// Releases the old slot and validates the new slot via Schedule Service.
    /// </summary>
    Task<AppointmentDto> RescheduleAppointmentAsync(int appointmentId, RescheduleRequest request, CancellationToken ct = default);

    /// <summary>Marks a scheduled appointment as Completed.</summary>
    Task CompleteAppointmentAsync(int appointmentId, CancellationToken ct = default);

    /// <summary>Generic status override (Admin only).</summary>
    Task<string> UpdateStatusAsync(int appointmentId, string status, CancellationToken ct = default);
}
