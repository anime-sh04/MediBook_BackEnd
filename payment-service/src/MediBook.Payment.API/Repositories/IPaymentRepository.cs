namespace MediBook.Payment.API.Repositories;

/// <summary>
/// Data-access contract — matches the IPaymentRepository class diagram.
/// </summary>
public interface IPaymentRepository
{
    // ── Queries ───────────────────────────────────────────────────────────────

    Task<Entities.Payment?> FindByAppointmentIdAsync(int appointmentId, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.Payment>> FindBySlotIdAsync(int slotId, CancellationToken ct);

    Task<IReadOnlyList<Entities.Payment>> FindByPatientIdAsync(Guid patientId, CancellationToken ct = default);

    Task<IReadOnlyList<Entities.Payment>> FindByStatusAsync(string status, CancellationToken ct = default);

    Task<Entities.Payment?> FindByTransactionIdAsync(string transactionId, CancellationToken ct = default);

    Task<IReadOnlyList<Entities.Payment>> FindByProviderIdAsync(Guid providerId, CancellationToken ct = default);

    /// <summary>Sum of paid amounts for a given patient.</summary>
    Task<decimal> SumAmountByPatientIdAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>Payments with PaidAt within [from, to] (inclusive).</summary>
    Task<IReadOnlyList<Entities.Payment>> FindByPaidAtBetweenAsync(
        DateTime from, DateTime to, CancellationToken ct = default);

    Task<Entities.Payment?> GetByIdAsync(int paymentId, CancellationToken ct = default);

    /// <summary>Tracked fetch — required before calling entity mutating methods and SaveChanges.</summary>
    Task<Entities.Payment?> GetTrackedByIdAsync(int paymentId, CancellationToken ct = default);

    /// <summary>Sum of paid amounts for a given provider.</summary>
    Task<decimal> SumAmountByProviderIdAsync(Guid providerId, CancellationToken ct = default);

    // ── Mutations ─────────────────────────────────────────────────────────────

    Task<Entities.Payment> AddAsync(Entities.Payment payment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
