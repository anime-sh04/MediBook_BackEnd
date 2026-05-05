using MediBook.Payment.API.Data;
using Microsoft.EntityFrameworkCore;

namespace MediBook.Payment.API.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _db;

    public PaymentRepository(PaymentDbContext db) => _db = db;

    public async Task<Entities.Payment?> FindByAppointmentIdAsync(int appointmentId, CancellationToken ct = default)
        => await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.AppointmentId == appointmentId, ct);

    public async Task<IReadOnlyList<Entities.Payment>> FindBySlotIdAsync(int slotId, CancellationToken ct)
    {
        return await _db.Payments
            .Where(x => x.SlotId == slotId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Entities.Payment>> FindByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _db.Payments.AsNoTracking().Where(p => p.PatientId == patientId).OrderByDescending(p => p.PaidAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Entities.Payment>> FindByStatusAsync(string status, CancellationToken ct = default)
        => await _db.Payments.AsNoTracking().Where(p => p.Status == status).OrderByDescending(p => p.PaidAt).ToListAsync(ct);

    public async Task<Entities.Payment?> FindByTransactionIdAsync(string transactionId, CancellationToken ct = default)
        => await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.TransactionId == transactionId, ct);

    public async Task<IReadOnlyList<Entities.Payment>> FindByProviderIdAsync(Guid providerId, CancellationToken ct = default)
        => await _db.Payments.AsNoTracking().Where(p => p.ProviderId == providerId).OrderByDescending(p => p.PaidAt).ToListAsync(ct);

    public async Task<decimal> SumAmountByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _db.Payments.Where(p => p.PatientId == patientId && p.Status == Entities.Payment.StatusPaid).SumAsync(p => p.Amount, ct);

    public async Task<IReadOnlyList<Entities.Payment>> FindByPaidAtBetweenAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await _db.Payments.AsNoTracking().Where(p => p.PaidAt >= from && p.PaidAt <= to).OrderByDescending(p => p.PaidAt).ToListAsync(ct);

    public async Task<Entities.Payment?> GetByIdAsync(int paymentId, CancellationToken ct = default)
        => await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.PaymentId == paymentId, ct);

    public async Task<Entities.Payment?> GetTrackedByIdAsync(int paymentId, CancellationToken ct = default)
        => await _db.Payments.FirstOrDefaultAsync(p => p.PaymentId == paymentId, ct);

    public async Task<decimal> SumAmountByProviderIdAsync(Guid providerId, CancellationToken ct = default)
        => await _db.Payments.Where(p => p.ProviderId == providerId && p.Status == Entities.Payment.StatusPaid).SumAsync(p => p.Amount, ct);

    public async Task<Entities.Payment> AddAsync(Entities.Payment payment, CancellationToken ct = default)
    {
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);
        return payment;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
