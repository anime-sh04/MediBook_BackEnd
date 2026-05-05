namespace MediBook.Review.API.Repositories;

/// <summary>
/// Data-access contract — matches the IReviewRepository class diagram.
/// </summary>
public interface IReviewRepository
{
    // ── Queries ───────────────────────────────────────────────────────────────

    Task<IReadOnlyList<Entities.Review>> FindByProviderIdAsync(Guid providerId, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.Review>> FindByPatientIdAsync(Guid patientId, CancellationToken ct = default);

    /// <returns>The review for the given appointment, or null.</returns>
    Task<Entities.Review?> FindByAppointmentIdAsync(int appointmentId, CancellationToken ct = default);

    /// <summary>Computes the average rating for a provider across all their reviews.</summary>
    Task<double> AvgRatingByProviderIdAsync(Guid providerId, CancellationToken ct = default);

    Task<IReadOnlyList<Entities.Review>> FindByRatingAsync(int rating, CancellationToken ct = default);
    Task<int>  CountByProviderIdAsync(Guid providerId, CancellationToken ct = default);

    /// <summary>Returns true when a review already exists for the given appointment.</summary>
    Task<bool> ExistsByAppointmentIdAsync(int appointmentId, CancellationToken ct = default);

    Task<Entities.Review?> GetByIdAsync(int reviewId, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.Review>> GetAllAsync(CancellationToken ct = default);

    // ── Mutations ─────────────────────────────────────────────────────────────

    Task<Entities.Review> AddAsync(Entities.Review review, CancellationToken ct = default);
    Task DeleteByReviewIdAsync(int reviewId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
