using MediBook.Review.API.DTOs;

namespace MediBook.Review.API.Services;

/// <summary>
/// Business contract — matches the IReviewService class diagram.
/// Declares all review CRUD, average rating computation, and moderation operations.
/// </summary>
public interface IReviewService
{
    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a new review for a completed appointment.
    /// Enforces one-review-per-appointment (throws InvalidOperationException if duplicate).
    /// </summary>
    Task<ReviewDto> AddReviewAsync(AddReviewRequest request, CancellationToken ct = default);

    /// <summary>Updates the rating and comment of an existing review.</summary>
    Task<ReviewDto> UpdateReviewAsync(int reviewId, UpdateReviewRequest request, CancellationToken ct = default);

    /// <summary>Deletes a review (admin moderation or patient retraction).</summary>
    Task DeleteReviewAsync(int reviewId, CancellationToken ct = default);

    // ── Reads ─────────────────────────────────────────────────────────────────

    Task<IReadOnlyList<ReviewDto>> GetByProviderAsync(Guid providerId, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewDto>> GetByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <returns>The review for the given appointment, or null.</returns>
    Task<ReviewDto?> GetByAppointmentAsync(int appointmentId, CancellationToken ct = default);

    Task<IReadOnlyList<ReviewDto>> GetAllReviewsAsync(CancellationToken ct = default);

    // ── Aggregates ────────────────────────────────────────────────────────────

    /// <summary>Returns the average star rating for a provider (0.0 if no reviews).</summary>
    Task<AvgRatingDto> GetAvgRatingAsync(Guid providerId, CancellationToken ct = default);

    /// <summary>Returns the total number of reviews for a provider.</summary>
    Task<int> GetReviewCountAsync(Guid providerId, CancellationToken ct = default);
}
