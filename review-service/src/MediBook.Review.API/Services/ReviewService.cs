using MediBook.Review.API.DTOs;
using MediBook.Review.API.Repositories;

namespace MediBook.Review.API.Services;

/// <summary>
/// Implements all review CRUD, average rating computation, and moderation operations.
/// After any mutation that affects ratings, it notifies the provider-service
/// to keep the AvgRating field on the Provider entity in sync.
/// </summary>
public sealed class ReviewService : IReviewService
{
    private readonly IReviewRepository          _repo;
    private readonly IProviderClient            _providerSvc;
    private readonly ILogger<ReviewService>     _logger;

    public ReviewService(
        IReviewRepository      repo,
        IProviderClient        providerSvc,
        ILogger<ReviewService> logger)
    {
        _repo        = repo;
        _providerSvc = providerSvc;
        _logger      = logger;
    }

    // ── AddReview ─────────────────────────────────────────────────────────────

    public async Task<ReviewDto> AddReviewAsync(
        AddReviewRequest request, CancellationToken ct = default)
    {
        // Enforce: one review per appointment
        if (await _repo.ExistsByAppointmentIdAsync(request.AppointmentId, ct))
            throw new InvalidOperationException(
                $"A review already exists for appointment {request.AppointmentId}. " +
                "Only one review is permitted per appointment.");

        var review = Entities.Review.Create(
            request.AppointmentId,
            request.PatientId,
            request.ProviderId,
            request.Rating,
            request.Comment,
            request.IsAnonymous);

        var saved = await _repo.AddAsync(review, ct);

        _logger.LogInformation(
            "Review {ReviewId} added by patient {PatientId} for provider {ProviderId} (rating {Rating}).",
            saved.ReviewId, saved.PatientId, saved.ProviderId, saved.Rating);

        // Push updated avg rating to provider-service (best-effort)
        await SyncProviderRatingAsync(saved.ProviderId, ct);

        return ToDto(saved);
    }

    // ── UpdateReview ──────────────────────────────────────────────────────────

    public async Task<ReviewDto> UpdateReviewAsync(
        int reviewId, UpdateReviewRequest request, CancellationToken ct = default)
    {
        var review = await _repo.GetByIdAsync(reviewId, ct)
            ?? throw new KeyNotFoundException($"Review {reviewId} not found.");

        review.Update(request.Rating, request.Comment);
        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Review {ReviewId} updated (new rating {Rating}).", reviewId, request.Rating);

        await SyncProviderRatingAsync(review.ProviderId, ct);

        return ToDto(review);
    }

    // ── DeleteReview ──────────────────────────────────────────────────────────

    public async Task DeleteReviewAsync(int reviewId, CancellationToken ct = default)
    {
        var review = await _repo.GetByIdAsync(reviewId, ct)
            ?? throw new KeyNotFoundException($"Review {reviewId} not found.");

        var providerId = review.ProviderId;

        await _repo.DeleteByReviewIdAsync(reviewId, ct);

        _logger.LogInformation("Review {ReviewId} deleted.", reviewId);

        // Re-sync provider rating after deletion
        await SyncProviderRatingAsync(providerId, ct);
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ReviewDto>> GetByProviderAsync(
        Guid providerId, CancellationToken ct = default)
    {
        var reviews = await _repo.FindByProviderIdAsync(providerId, ct);
        return reviews.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ReviewDto>> GetByPatientAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var reviews = await _repo.FindByPatientIdAsync(patientId, ct);
        return reviews.Select(ToDto).ToList();
    }

    public async Task<ReviewDto?> GetByAppointmentAsync(
        int appointmentId, CancellationToken ct = default)
    {
        var review = await _repo.FindByAppointmentIdAsync(appointmentId, ct);
        return review is null ? null : ToDto(review);
    }

    public async Task<IReadOnlyList<ReviewDto>> GetAllReviewsAsync(
        CancellationToken ct = default)
    {
        var reviews = await _repo.GetAllAsync(ct);
        return reviews.Select(ToDto).ToList();
    }

    // ── Aggregates ────────────────────────────────────────────────────────────

    public async Task<AvgRatingDto> GetAvgRatingAsync(
        Guid providerId, CancellationToken ct = default)
    {
        var avg   = await _repo.AvgRatingByProviderIdAsync(providerId, ct);
        var count = await _repo.CountByProviderIdAsync(providerId, ct);

        return new AvgRatingDto(
            ProviderId:  providerId,
            AvgRating:   Math.Round(avg, 2),
            ReviewCount: count);
    }

    public async Task<int> GetReviewCountAsync(
        Guid providerId, CancellationToken ct = default) =>
        await _repo.CountByProviderIdAsync(providerId, ct);

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes the average rating and pushes it to the provider-service.
    /// Failures are logged but do not propagate — they are best-effort.
    /// </summary>
    private async Task SyncProviderRatingAsync(Guid providerId, CancellationToken ct)
    {
        try
        {
            var avg = await _repo.AvgRatingByProviderIdAsync(providerId, ct);
            await _providerSvc.UpdateProviderRatingAsync(providerId, Math.Round(avg, 2), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sync avg rating for provider {ProviderId}.", providerId);
        }
    }

    /// <summary>
    /// Maps a Review entity to a ReviewDto.
    /// PatientId is always included — callers/controllers enforce access control.
    /// </summary>
    private static ReviewDto ToDto(Entities.Review r) => new(
        ReviewId:      r.ReviewId,
        AppointmentId: r.AppointmentId,
        PatientId:     r.IsAnonymous ? null : r.PatientId,
        ProviderId:    r.ProviderId,
        Rating:        r.Rating,
        Comment:       r.Comment,
        ReviewDate:    r.ReviewDate.ToString("yyyy-MM-dd"),
        IsVerified:    r.IsVerified,
        IsAnonymous:   r.IsAnonymous
    );
}
