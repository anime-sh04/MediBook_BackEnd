namespace MediBook.Schedule.API.Entities;

/// <summary>
/// Represents a single provider time slot.
/// Slot lifecycle: Available → Booked → Released (unbook) | Blocked → Unblocked
/// </summary>
public sealed class AvailabilitySlot
{
    public int      SlotId          { get; private set; }
    public Guid      ProviderId      { get; private set; }
    public DateOnly Date            { get; private set; }
    public TimeOnly StartTime       { get; private set; }
    public TimeOnly EndTime         { get; private set; }
    public decimal Price { get; private set; }
    public int      DurationMinutes { get; private set; }
    public bool     IsBooked        { get; private set; }
    public bool     IsBlocked       { get; private set; }
    public string   Recurrence      { get; private set; } = "none";
    public DateTime CreatedAt       { get; private set; }

    private AvailabilitySlot() { } // EF Core

    // ── Factory ───────────────────────────────────────────────────────────────

    public static AvailabilitySlot Create(
        Guid      providerId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        string   recurrence,
        decimal price)
    {
        if (endTime <= startTime)
            throw new ArgumentException("EndTime must be after StartTime.");

        int duration = (int)(endTime.ToTimeSpan() - startTime.ToTimeSpan()).TotalMinutes;

        return new AvailabilitySlot
        {
            ProviderId      = providerId,
            Date            = date,
            StartTime       = startTime,
            EndTime         = endTime,
            DurationMinutes = duration,
            IsBooked        = false,
            IsBlocked       = false,
            Recurrence      = (recurrence ?? "none").Trim().ToLowerInvariant(),
            CreatedAt       = DateTime.UtcNow,
            Price           = price
        };
    }

    // ── State transitions ─────────────────────────────────────────────────────

    /// <summary>Marks the slot as booked (Available → Booked).</summary>
    /// <exception cref="InvalidOperationException">Slot is already booked or blocked.</exception>
    public void Book()
    {
        if (IsBooked)  throw new InvalidOperationException($"Slot {SlotId} is already booked.");
        if (IsBlocked) throw new InvalidOperationException($"Slot {SlotId} is blocked and cannot be booked.");
        IsBooked = true;
    }

    /// <summary>Releases the slot back to available (Booked → Available).</summary>
    /// <exception cref="InvalidOperationException">Slot is not currently booked.</exception>
    public void Unbook()
    {
        if (!IsBooked) throw new InvalidOperationException($"Slot {SlotId} is not booked.");
        IsBooked = false;
    }

    /// <summary>Blocks the slot (e.g. provider leave). Unbooks first if needed.</summary>
    public void Block()
    {
        IsBooked  = false; // release any booking before blocking
        IsBlocked = true;
    }

    /// <summary>Removes the block, making the slot available again.</summary>
    public void Unblock()
    {
        IsBlocked = false;
    }

    /// <summary>Updates date/time details (only allowed when not booked or blocked).</summary>
    public void Update(DateOnly date, TimeOnly startTime, TimeOnly endTime, string? recurrence = null)
    {
        if (IsBooked)  throw new InvalidOperationException("Cannot update a booked slot.");
        if (IsBlocked) throw new InvalidOperationException("Cannot update a blocked slot.");
        if (endTime <= startTime)
            throw new ArgumentException("EndTime must be after StartTime.");

        Date            = date;
        StartTime       = startTime;
        EndTime         = endTime;
        DurationMinutes = (int)(endTime.ToTimeSpan() - startTime.ToTimeSpan()).TotalMinutes;
        if (recurrence is not null)
            Recurrence = recurrence.Trim().ToLowerInvariant();
    }

    // ── Read helpers (match class diagram) ────────────────────────────────────

    public int  GetSlotId()   => SlotId;
}
