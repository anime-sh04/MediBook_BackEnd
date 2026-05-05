using MediBook.Schedule.API.DTOs;

namespace MediBook.Schedule.API.Services;

/// <summary>
/// Business contract for slot CRUD, booking state management,
/// bulk creation, and recurrence generation — matches the class diagram.
/// </summary>
public interface IScheduleService
{
    // ── Slot CRUD ─────────────────────────────────────────────────────────────

    /// <summary>Creates a single availability slot.</summary>
    Task<AvailabilitySlotDto> AddSlotAsync(AddSlotRequest request, CancellationToken ct = default);

    /// <summary>Creates multiple availability slots in one operation.</summary>
    Task<IReadOnlyList<AvailabilitySlotDto>> AddBulkSlotsAsync(AddBulkSlotsRequest request, CancellationToken ct = default);

    /// <summary>Returns all slots (any state) for a provider.</summary>
    Task<IReadOnlyList<AvailabilitySlotDto>> GetSlotsByProviderAsync(Guid providerId, CancellationToken ct = default);

    /// <summary>Returns unbooked, unblocked slots for a provider on a specific date.</summary>
    Task<IReadOnlyList<AvailabilitySlotDto>> GetAvailableSlotsAsync(Guid providerId, DateOnly date, CancellationToken ct = default);

    /// <summary>Returns a single slot by SlotId.</summary>
    /// <exception cref="KeyNotFoundException">Slot not found.</exception>
    Task<AvailabilitySlotDto?> GetSlotByIdAsync(int slotId, CancellationToken ct = default);

    /// <summary>Updates date/time details of an existing slot.</summary>
    /// <exception cref="KeyNotFoundException">Slot not found.</exception>
    /// <exception cref="InvalidOperationException">Slot is booked or blocked.</exception>
    Task<AvailabilitySlotDto> UpdateSlotAsync(int slotId, UpdateSlotRequest request, CancellationToken ct = default);

    /// <summary>Hard-deletes a slot.</summary>
    /// <exception cref="KeyNotFoundException">Slot not found.</exception>
    Task DeleteSlotAsync(int slotId, CancellationToken ct = default);

    // ── Booking state management ──────────────────────────────────────────────

    /// <summary>Transitions the slot: Available → Booked.</summary>
    /// <exception cref="KeyNotFoundException">Slot not found.</exception>
    /// <exception cref="InvalidOperationException">Slot is already booked or blocked.</exception>
    Task BookSlotAsync(int slotId, CancellationToken ct = default);

    /// <summary>Transitions the slot: Booked → Available (unbook/release).</summary>
    /// <exception cref="KeyNotFoundException">Slot not found.</exception>
    Task UnbookSlotAsync(int slotId, CancellationToken ct = default);

    /// <summary>Blocks the slot (e.g. provider leave). Releases any active booking.</summary>
    /// <exception cref="KeyNotFoundException">Slot not found.</exception>
    Task BlockSlotAsync(int slotId, CancellationToken ct = default);

    /// <summary>Removes the block from a slot, returning it to available.</summary>
    /// <exception cref="KeyNotFoundException">Slot not found.</exception>
    Task UnblockSlotAsync(int slotId, CancellationToken ct = default);


    // ── Saga: payment-gated booking ───────────────────────────────────────────

    /// <summary>
    /// ★ SAGA ENTRY POINT — called by PUT /api/v1/slots/{id}/book.
    /// Marks the slot as PENDING and publishes a PaymentRequested event.
    /// The slot will be CONFIRMED (IsBooked=true) when PaymentSucceeded arrives,
    /// or rolled back to AVAILABLE when PaymentFailed arrives.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Slot not found.</exception>
    /// <exception cref="InvalidOperationException">Slot is already booked or blocked.</exception>
    Task<Guid> InitiateBookingAsync(int slotId, BookSlotRequest request, CancellationToken ct = default);

    // ── Recurrence generation ─────────────────────────────────────────────────

    /// <summary>
    /// Generates recurring slots for a provider between StartDate and EndDate using
    /// the specified pattern ("daily" or "weekly").
    /// </summary>
    Task<IReadOnlyList<AvailabilitySlotDto>> GenerateRecurringSlotsAsync(
        GenerateRecurringRequest request, CancellationToken ct = default);
}
