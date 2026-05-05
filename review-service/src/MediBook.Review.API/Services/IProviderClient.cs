namespace MediBook.Review.API.Services;

/// <summary>
/// Typed HTTP client for the provider-service.
/// Used to push updated average ratings after a review is added, updated, or deleted.
/// </summary>
public interface IProviderClient
{
    /// <summary>
    /// Calls PUT /api/v1/providers/{providerId}/rating on the provider-service
    /// to update the cached AvgRating field.
    /// </summary>
    Task UpdateProviderRatingAsync(Guid providerId, double avgRating, CancellationToken ct = default);
}
