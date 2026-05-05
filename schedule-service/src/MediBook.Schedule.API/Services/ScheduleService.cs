using MediBook.Schedule.API.DTOs;
using MediBook.Schedule.API.Entities;
using MediBook.Schedule.API.Messaging.Contracts;
using MediBook.Schedule.API.Messaging.Infrastructure;
using MediBook.Schedule.API.Repositories;


namespace MediBook.Schedule.API.Services;

public sealed class ScheduleService : IScheduleService
{
    private readonly ISlotRepository          _repo;
    private readonly SagaEventPublisher       _publisher;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(
        ISlotRepository          repo,
        SagaEventPublisher       publisher,
        ILogger<ScheduleService> logger)
    {
        _repo      = repo;
        _publisher = publisher;
        _logger    = logger;
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    public async Task<AvailabilitySlotDto> AddSlotAsync(
        AddSlotRequest request, CancellationToken ct = default)
    {
        var slot = AvailabilitySlot.Create(
            request.ProviderId,
            ParseDate(request.Date),
            ParseTime(request.StartTime),
            ParseTime(request.EndTime),
            request.Recurrence,
            request.Price);

        var saved = await _repo.AddAsync(slot, ct);
        _logger.LogInformation("Slot created. SlotId: {SlotId}, ProviderId: {ProviderId}", saved.SlotId, saved.ProviderId);
        return MapToDto(saved);
    }

    public async Task<IReadOnlyList<AvailabilitySlotDto>> AddBulkSlotsAsync(
        AddBulkSlotsRequest request, CancellationToken ct = default)
    {
        var slots = request.Slots.Select(r => AvailabilitySlot.Create(
            r.ProviderId,
            ParseDate(r.Date),
            ParseTime(r.StartTime),
            ParseTime(r.EndTime),
            r.Recurrence,
            r.Price)).ToList();

        var saved = await _repo.AddBulkAsync(slots, ct);
        _logger.LogInformation("Bulk-created {Count} slots for ProviderId: {ProviderId}",
            saved.Count, request.Slots.FirstOrDefault()?.ProviderId);
        return saved.Select(MapToDto).ToList();
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AvailabilitySlotDto>> GetSlotsByProviderAsync(
        Guid providerId, CancellationToken ct = default)
    {
        var slots = await _repo.FindByProviderIdAsync(providerId, ct);
        return slots.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AvailabilitySlotDto>> GetAvailableSlotsAsync(
        Guid providerId, DateOnly date, CancellationToken ct = default)
    {
        var slots = await _repo.FindAvailableByProviderAndDateAsync(providerId, date, ct);
        return slots.Select(MapToDto).ToList();
    }

    public async Task<AvailabilitySlotDto?> GetSlotByIdAsync(
        int slotId, CancellationToken ct = default)
    {
        var slot = await _repo.GetByIdAsync(slotId, ct)
            ?? throw new KeyNotFoundException($"Slot {slotId} not found.");
        return MapToDto(slot);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<AvailabilitySlotDto> UpdateSlotAsync(
        int slotId, UpdateSlotRequest request, CancellationToken ct = default)
    {
        var slot = await _repo.GetByIdAsync(slotId, ct)
            ?? throw new KeyNotFoundException($"Slot {slotId} not found.");

        slot.Update(
            ParseDate(request.Date),
            ParseTime(request.StartTime),
            ParseTime(request.EndTime),
            request.Recurrence);

        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Slot {SlotId} updated.", slotId);
        return MapToDto(slot);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task DeleteSlotAsync(int slotId, CancellationToken ct = default)
    {
        await _repo.DeleteBySlotIdAsync(slotId, ct);
        _logger.LogInformation("Slot {SlotId} deleted.", slotId);
    }

    // ── Booking state management ──────────────────────────────────────────────

    public async Task BookSlotAsync(int slotId, CancellationToken ct = default)
    {
        var slot = await RequireSlotAsync(slotId, ct);
        slot.Book();
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Slot {SlotId} booked.", slotId);
    }

    public async Task UnbookSlotAsync(int slotId, CancellationToken ct = default)
    {
        var slot = await RequireSlotAsync(slotId, ct);
        slot.Unbook();
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Slot {SlotId} unbooked (released).", slotId);
    }

    public async Task BlockSlotAsync(int slotId, CancellationToken ct = default)
    {
        var slot = await RequireSlotAsync(slotId, ct);
        slot.Block();
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Slot {SlotId} blocked.", slotId);
    }

    public async Task UnblockSlotAsync(int slotId, CancellationToken ct = default)
    {
        var slot = await RequireSlotAsync(slotId, ct);
        slot.Unblock();
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Slot {SlotId} unblocked.", slotId);
    }

    // ── Recurrence generation ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<AvailabilitySlotDto>> GenerateRecurringSlotsAsync(
        GenerateRecurringRequest request, CancellationToken ct = default)
    {
        DateOnly start     = ParseDate(request.StartDate);
        DateOnly end       = ParseDate(request.EndDate);
        TimeOnly slotStart = ParseTime(request.SlotStartTime);
        TimeOnly slotEnd   = ParseTime(request.SlotEndTime);

        if (end < start)
            throw new ArgumentException("EndDate must be on or after StartDate.");

        string pattern = request.Recurrence.Trim().ToLowerInvariant();
        if (pattern is not ("daily" or "weekly"))
            throw new ArgumentException("Recurrence must be 'daily' or 'weekly'.");

        var slots = new List<AvailabilitySlot>();
        DateOnly current = start;

        while (current <= end)
        {
            slots.Add(AvailabilitySlot.Create(request.ProviderId, current, slotStart, slotEnd, pattern,request.Price ));
            current = pattern == "daily" ? current.AddDays(1) : current.AddDays(7);
        }

        if (slots.Count == 0)
            return Array.Empty<AvailabilitySlotDto>();

        var saved = await _repo.AddBulkAsync(slots, ct);
        _logger.LogInformation(
            "Generated {Count} {Pattern} recurring slots for ProviderId: {ProviderId}",
            saved.Count, pattern, request.ProviderId);

        return saved.Select(MapToDto).ToList();
    }


    // ── Saga: payment-gated booking ───────────────────────────────────────────

    /// <summary>
    /// ★ SAGA ENTRY POINT
    ///
    /// Step 1: Validate the slot is available.
    /// Step 2: Mark the slot as PENDING (IsBooked stays false — set to true only
    ///         on PaymentSucceeded to prevent double-booking on failure).
    /// Step 3: Publish PaymentRequested with full appointment context embedded,
    ///         so the Appointment Service can create a complete record on success
    ///         without any cross-service HTTP calls.
    ///
    /// Returns the CorrelationId so the caller can track the Saga instance.
    /// </summary>
    public async Task<Guid> InitiateBookingAsync(
        int slotId, BookSlotRequest request, CancellationToken ct = default)
    {
        var slot = await RequireSlotAsync(slotId, ct);

        if (slot.IsBooked)
            throw new InvalidOperationException($"Slot {slotId} is already booked.");
        if (slot.IsBlocked)
            throw new InvalidOperationException($"Slot {slotId} is blocked and cannot be booked.");

        var correlationId = Guid.NewGuid();

        _logger.LogInformation(
            "[Schedule] ★ SAGA INITIATED — SlotId={SlotId}, CorrelationId={CorrelationId}, " +
            "PatientId={PatientId}, Amount={Amount}",
            slotId, correlationId, request.PatientId, slot.Price);

        // ── Publish PaymentRequested (SAGA START) ─────────────────────────────
        // Slot date/time are embedded directly from the entity so downstream
        // services receive the full appointment context in a single event.
        var @event = new PaymentRequested
        {
            CorrelationId      = correlationId,
            SlotId             = slotId,
            PatientId          = request.PatientId,
            ProviderId         = slot.ProviderId,
            Amount             = slot.Price,
            Mode               = request.Mode,
            Currency           = request.Currency,
            Notes              = request.Notes,

            // Appointment fields sourced from the slot entity
            AppointmentDate    = slot.Date.ToString("yyyy-MM-dd"),
            StartTime          = slot.StartTime.ToString("HH:mm"),
            EndTime            = slot.EndTime.ToString("HH:mm"),
            ServiceType        = request.ServiceType,
            ModeOfConsultation = request.ModeOfConsultation
        };

        _publisher.PublishPaymentRequested(@event);

        return correlationId;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<AvailabilitySlot> RequireSlotAsync(int slotId, CancellationToken ct)
        => await _repo.GetByIdAsync(slotId, ct)
           ?? throw new KeyNotFoundException($"Slot {slotId} not found.");

    private static DateOnly ParseDate(string s)
    {
        if (!DateOnly.TryParse(s, out var d))
            throw new ArgumentException($"Invalid date format: '{s}'. Expected yyyy-MM-dd.");
        return d;
    }

    private static TimeOnly ParseTime(string s)
    {
        if (!TimeOnly.TryParse(s, out var t))
            throw new ArgumentException($"Invalid time format: '{s}'. Expected HH:mm.");
        return t;
    }

    private static AvailabilitySlotDto MapToDto(AvailabilitySlot s) => new(
        s.SlotId,
        s.ProviderId,
        s.Date.ToString("yyyy-MM-dd"),
        s.StartTime.ToString("HH:mm"),
        s.EndTime.ToString("HH:mm"),
        s.DurationMinutes,
        s.IsBooked,
        s.IsBlocked,
        s.Recurrence,
        s.CreatedAt);
}
