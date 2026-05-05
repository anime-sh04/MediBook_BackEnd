using MediBook.Schedule.API.Data;
using MediBook.Schedule.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediBook.Schedule.API.Repositories;

public sealed class SlotRepository : ISlotRepository
{
    private readonly ScheduleDbContext _db;

    public SlotRepository(ScheduleDbContext db) => _db = db;

    public async Task<IReadOnlyList<AvailabilitySlot>> FindByProviderIdAsync(
        Guid providerId, CancellationToken ct = default) =>
        await _db.AvailabilitySlots
                 .AsNoTracking()
                 .Where(s => s.ProviderId == providerId)
                 .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<AvailabilitySlot>> FindByProviderIdAndDateAsync(
        Guid providerId, DateOnly date, CancellationToken ct = default) =>
        await _db.AvailabilitySlots
                 .AsNoTracking()
                 .Where(s => s.ProviderId == providerId && s.Date == date)
                 .OrderBy(s => s.StartTime)
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<AvailabilitySlot>> FindAvailableByProviderAndDateAsync(
        Guid providerId, DateOnly date, CancellationToken ct = default) =>
        await _db.AvailabilitySlots
                 .AsNoTracking()
                 .Where(s => s.ProviderId == providerId &&
                             s.Date       == date       &&
                             !s.IsBooked  && !s.IsBlocked)
                 .OrderBy(s => s.StartTime)
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<AvailabilitySlot>> FindByDateBetweenAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await _db.AvailabilitySlots
                 .AsNoTracking()
                 .Where(s => s.Date >= from && s.Date <= to)
                 .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
                 .ToListAsync(ct);

    public async Task<int> CountAvailableByProviderIdAsync(
        Guid providerId, CancellationToken ct = default) =>
        await _db.AvailabilitySlots
                 .CountAsync(s => s.ProviderId == providerId &&
                                  !s.IsBooked  && !s.IsBlocked, ct);

    public async Task<AvailabilitySlot?> GetByIdAsync(
        int slotId, CancellationToken ct = default) =>
        await _db.AvailabilitySlots
                 .FirstOrDefaultAsync(s => s.SlotId == slotId, ct);

    public async Task<AvailabilitySlot> AddAsync(
        AvailabilitySlot slot, CancellationToken ct = default)
    {
        _db.AvailabilitySlots.Add(slot);
        await _db.SaveChangesAsync(ct);
        return slot;
    }

    public async Task<IReadOnlyList<AvailabilitySlot>> AddBulkAsync(
        IEnumerable<AvailabilitySlot> slots, CancellationToken ct = default)
    {
        var list = slots.ToList();
        _db.AvailabilitySlots.AddRange(list);
        await _db.SaveChangesAsync(ct);
        return list;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);

    public async Task DeleteBySlotIdAsync(int slotId, CancellationToken ct = default)
    {
        var slot = await _db.AvailabilitySlots
                            .FirstOrDefaultAsync(s => s.SlotId == slotId, ct)
                  ?? throw new KeyNotFoundException($"Slot {slotId} not found.");

        _db.AvailabilitySlots.Remove(slot);
        await _db.SaveChangesAsync(ct);
    }
}
