namespace MediBook.Appointment.API.Repositories;

/// <summary>
/// Data-access contract — matches the IAppointmentRepository class diagram.
/// </summary>
public interface IAppointmentRepository
{
    // ── Queries ───────────────────────────────────────────────────────────────

    Task<IReadOnlyList<Entities.Appointment>> FindByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.Appointment>> FindByProviderIdAsync(Guid providerId, CancellationToken ct = default);

    /// <returns>The appointment linked to the given slot, or null.</returns>
    Task<Entities.Appointment?> FindBySlotIdAsync(int slotId, CancellationToken ct = default);

    Task<IReadOnlyList<Entities.Appointment>> FindByStatusAsync(string status, CancellationToken ct = default);

    Task<IReadOnlyList<Entities.Appointment>> FindByProviderIdAndAppointmentDateAsync(
        Guid providerId, DateOnly date, CancellationToken ct = default);

    /// <summary>Returns upcoming (Scheduled, future-date) appointments for a patient.</summary>
    Task<IReadOnlyList<Entities.Appointment>> FindUpcomingByPatientIdAsync(Guid patientId, CancellationToken ct = default);

    Task<int> CountByProviderIdAsync(Guid providerId, CancellationToken ct = default);

    Task<Entities.Appointment?> GetByIdAsync(int appointmentId, CancellationToken ct = default);

    // ── Mutations ─────────────────────────────────────────────────────────────

    Task<Entities.Appointment> AddAsync(Entities.Appointment appointment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
