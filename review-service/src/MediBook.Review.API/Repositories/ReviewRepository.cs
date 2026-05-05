using MediBook.Review.API.Data;
using Microsoft.EntityFrameworkCore;

namespace MediBook.Review.API.Repositories;

public sealed class ReviewRepository : IReviewRepository
{
    private readonly ReviewDbContext _db;

    public ReviewRepository(ReviewDbContext db) => _db = db;

    public async Task<IReadOnlyList<Entities.Review>> FindByProviderIdAsync(
        Guid providerId, CancellationToken ct = default) =>
        await _db.Reviews
                 .AsNoTracking()
                 .Where(r => r.ProviderId == providerId)
                 .OrderByDescending(r => r.ReviewDate)
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<Entities.Review>> FindByPatientIdAsync(
        Guid patientId, CancellationToken ct = default) =>
        await _db.Reviews
                 .AsNoTracking()
                 .Where(r => r.PatientId == patientId)
                 .OrderByDescending(r => r.ReviewDate)
                 .ToListAsync(ct);

    public async Task<Entities.Review?> FindByAppointmentIdAsync(
        int appointmentId, CancellationToken ct = default) =>
        await _db.Reviews
                 .AsNoTracking()
                 .FirstOrDefaultAsync(r => r.AppointmentId == appointmentId, ct);

    public async Task<double> AvgRatingByProviderIdAsync(
        Guid providerId, CancellationToken ct = default)
    {
        var ratings = await _db.Reviews
                               .AsNoTracking()
                               .Where(r => r.ProviderId == providerId)
                               .Select(r => (double?)r.Rating)
                               .ToListAsync(ct);

        return ratings.Count == 0 ? 0.0 : ratings.Average() ?? 0.0;
    }

    public async Task<IReadOnlyList<Entities.Review>> FindByRatingAsync(
        int rating, CancellationToken ct = default) =>
        await _db.Reviews
                 .AsNoTracking()
                 .Where(r => r.Rating == rating)
                 .OrderByDescending(r => r.ReviewDate)
                 .ToListAsync(ct);

    public async Task<int> CountByProviderIdAsync(
        Guid providerId, CancellationToken ct = default) =>
        await _db.Reviews.CountAsync(r => r.ProviderId == providerId, ct);

    public async Task<bool> ExistsByAppointmentIdAsync(
        int appointmentId, CancellationToken ct = default) =>
        await _db.Reviews.AnyAsync(r => r.AppointmentId == appointmentId, ct);

    public async Task<Entities.Review?> GetByIdAsync(
        int reviewId, CancellationToken ct = default) =>
        await _db.Reviews
                 .FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct);

    public async Task<IReadOnlyList<Entities.Review>> GetAllAsync(
        CancellationToken ct = default) =>
        await _db.Reviews
                 .AsNoTracking()
                 .OrderByDescending(r => r.ReviewDate)
                 .ToListAsync(ct);

    public async Task<Entities.Review> AddAsync(Entities.Review review, CancellationToken ct = default)
    {
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(ct);
        return review;
    }

    public async Task DeleteByReviewIdAsync(int reviewId, CancellationToken ct = default)
    {
        var review = await _db.Reviews.FindAsync(new object[] { reviewId }, ct);
        if (review is null)
            throw new KeyNotFoundException($"Review {reviewId} not found.");

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);
}
