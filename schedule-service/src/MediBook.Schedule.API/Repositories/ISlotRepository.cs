using MediBook.Schedule.API.Entities;

namespace MediBook.Schedule.API.Repositories;

/// <summary>
/// Data-access contract for AvailabilitySlot — matches the ISlotRepository class diagram.
/// </summary>
public interface ISlotRepository
{
    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Returns all slots (any state) for the given provider.</summary>
    Task<IReadOnlyList<AvailabilitySlot>> FindByProviderIdAsync(Guid providerId, CancellationToken ct = default);

    /// <summary>Returns all slots for a provider on a specific date.</summary>
    Task<IReadOnlyList<AvailabilitySlot>> FindByProviderIdAndDateAsync(Guid providerId, DateOnly date, CancellationToken ct = default);

    /// <summary>Returns only unbooked, unblocked slots for a provider on a specific date.</summary>
    Task<IReadOnlyList<AvailabilitySlot>> FindAvailableByProviderAndDateAsync(Guid providerId, DateOnly date, CancellationToken ct = default);

    /// <summary>Returns all slots whose date falls within [from, to] (inclusive).</summary>
    Task<IReadOnlyList<AvailabilitySlot>> FindByDateBetweenAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Counts unbooked, unblocked slots for the given provider.</summary>
    Task<int> CountAvailableByProviderIdAsync(Guid providerId, CancellationToken ct = default);

    /// <summary>Returns a single slot by its SlotId, or null if not found.</summary>
    Task<AvailabilitySlot?> GetByIdAsync(int slotId, CancellationToken ct = default);

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>Persists a new slot.</summary>
    Task<AvailabilitySlot> AddAsync(AvailabilitySlot slot, CancellationToken ct = default);

    /// <summary>Persists a collection of new slots in a single batch.</summary>
    Task<IReadOnlyList<AvailabilitySlot>> AddBulkAsync(IEnumerable<AvailabilitySlot> slots, CancellationToken ct = default);

    /// <summary>Saves changes to an existing tracked slot.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Hard-deletes the slot with the given SlotId.</summary>
    Task DeleteBySlotIdAsync(int slotId, CancellationToken ct = default);
}
