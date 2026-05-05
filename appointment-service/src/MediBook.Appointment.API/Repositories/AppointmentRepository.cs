using MediBook.Appointment.API.Data;
using Microsoft.EntityFrameworkCore;

namespace MediBook.Appointment.API.Repositories;

public sealed class AppointmentRepository : IAppointmentRepository
{
    private readonly AppointmentDbContext _db;

    public AppointmentRepository(AppointmentDbContext db) => _db = db;

    public async Task<IReadOnlyList<Entities.Appointment>> FindByPatientIdAsync(
        Guid patientId, CancellationToken ct = default) =>
        await _db.Appointments
                 .AsNoTracking()
                 .Where(a => a.PatientId == patientId)
                 .OrderByDescending(a => a.AppointmentDate)
                 .ThenBy(a => a.StartTime)
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<Entities.Appointment>> FindByProviderIdAsync(
        Guid providerId, CancellationToken ct = default) =>
        await _db.Appointments
                 .AsNoTracking()
                 .Where(a => a.ProviderId == providerId)
                 .OrderByDescending(a => a.AppointmentDate)
                 .ThenBy(a => a.StartTime)
                 .ToListAsync(ct);

    public async Task<Entities.Appointment?> FindBySlotIdAsync(
        int slotId, CancellationToken ct = default) =>
        await _db.Appointments
                 .AsNoTracking()
                 .FirstOrDefaultAsync(a => a.SlotId == slotId, ct);

    public async Task<IReadOnlyList<Entities.Appointment>> FindByStatusAsync(
        string status, CancellationToken ct = default) =>
        await _db.Appointments
                 .AsNoTracking()
                 .Where(a => a.Status == status)
                 .OrderByDescending(a => a.AppointmentDate)
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<Entities.Appointment>> FindByProviderIdAndAppointmentDateAsync(
        Guid providerId, DateOnly date, CancellationToken ct = default) =>
        await _db.Appointments
                 .AsNoTracking()
                 .Where(a => a.ProviderId == providerId && a.AppointmentDate == date)
                 .OrderBy(a => a.StartTime)
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<Entities.Appointment>> FindUpcomingByPatientIdAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _db.Appointments
                        .AsNoTracking()
                        .Where(a => a.PatientId == patientId &&
                                    a.Status == Entities.Appointment.StatusScheduled &&
                                    a.AppointmentDate >= today)
                        .OrderBy(a => a.AppointmentDate)
                        .ThenBy(a => a.StartTime)
                        .ToListAsync(ct);
    }

    public async Task<int> CountByProviderIdAsync(
        Guid providerId, CancellationToken ct = default) =>
        await _db.Appointments.CountAsync(a => a.ProviderId == providerId, ct);

    public async Task<Entities.Appointment?> GetByIdAsync(
        int appointmentId, CancellationToken ct = default) =>
        await _db.Appointments
                 .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

    public async Task<Entities.Appointment> AddAsync(
        Entities.Appointment appointment, CancellationToken ct = default)
    {
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(ct);
        return appointment;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);
}
