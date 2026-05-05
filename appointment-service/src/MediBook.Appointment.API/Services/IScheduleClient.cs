using MediBook.Appointment.API.DTOs;

namespace MediBook.Appointment.API.Services;

/// <summary>
/// Typed HTTP client for communicating with the Schedule Service.
///
/// IMPORTANT — Saga design:
///   BookSlotAsync is intentionally NOT present here.
///   Slot booking is now driven exclusively by the PaymentSucceeded event.
///   The Schedule Service's PaymentResultConsumer calls slot.Book() when it
///   receives PaymentSucceeded.  The Appointment Service must never call
///   PUT /slots/{id}/book directly as that is the Saga entry point and
///   would start a second, orphaned Saga.
///
/// This client is used only for:
///   • GetSlotAsync   — reading slot details (e.g. for rescheduling validation)
///   • UnbookSlotAsync — releasing a slot on appointment cancellation
/// </summary>
public interface IScheduleClient
{
    /// <summary>Fetches slot details. Returns null if not found.</summary>
    Task<SlotDto?> GetSlotAsync(int slotId, CancellationToken ct = default);

    /// <summary>
    /// Releases a previously booked slot back to AVAILABLE.
    /// Called when an appointment is cancelled.
    /// </summary>
    Task UnbookSlotAsync(int slotId, CancellationToken ct = default);
}
