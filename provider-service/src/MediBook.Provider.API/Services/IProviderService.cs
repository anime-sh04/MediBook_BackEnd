using MediBook.Provider.API.DTOs;

namespace MediBook.Provider.API.Services;

/// <summary>
/// Business contract for the Provider service — matches the case study class diagram.
/// RegisterProvider | GetProviderById | GetBySpecialization | SearchProviders
/// UpdateProvider   | VerifyProvider  | SetAvailability     | DeleteProvider
/// UpdateRating     | GetAllProviders
/// </summary>
public interface IProviderService
{
    // ── Registration / CRUD ───────────────────────────────────────────────────

    /// <summary>Creates a provider profile linked to the given userId.</summary>
    /// <exception cref="InvalidOperationException">User already has a provider profile.</exception>
    Task<ProviderProfileDto> RegisterProviderAsync(Guid userId, RegisterProviderRequest request, CancellationToken ct = default);

    /// <summary>Returns a provider profile by its ProviderId.</summary>
    /// <exception cref="KeyNotFoundException">Profile not found.</exception>
    Task<ProviderProfileDto> GetProviderByIdAsync(Guid providerId, CancellationToken ct = default);

    /// <summary>Returns the provider profile linked to the authenticated user's UserId.</summary>
    /// <exception cref="KeyNotFoundException">Profile not found.</exception>
    Task<ProviderProfileDto> GetMyProfileAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Updates the provider profile for the authenticated user.</summary>
    /// <exception cref="KeyNotFoundException">Profile not found.</exception>
    Task<ProviderProfileDto> UpdateMyProfileAsync(Guid userId, RegisterProviderRequest request, CancellationToken ct = default);

    /// <summary>Hard-deletes a provider profile (Admin only).</summary>
    /// <exception cref="KeyNotFoundException">Profile not found.</exception>
    Task DeleteProviderAsync(Guid providerId, CancellationToken ct = default);

    // ── Search / Browse ───────────────────────────────────────────────────────

    /// <summary>Returns all verified, available providers — with optional filtering.</summary>
    Task<PagedResult<ProviderProfileDto>> GetAllProvidersAsync(ProviderSearchQuery query, CancellationToken ct = default);

    /// <summary>Filters providers by specialization (case-insensitive, partial match).</summary>
    Task<IReadOnlyList<ProviderProfileDto>> GetBySpecializationAsync(string specialization, CancellationToken ct = default);

    /// <summary>Full-text search across name, specialization, clinic name, and city.</summary>
    Task<IReadOnlyList<ProviderProfileDto>> SearchProvidersAsync(string searchTerm, CancellationToken ct = default);

    // ── Admin Actions ─────────────────────────────────────────────────────────

    /// <summary>Sets IsVerified = true/false for the given provider (Admin only).</summary>
    /// <exception cref="KeyNotFoundException">Profile not found.</exception>
    Task<ProviderProfileDto> VerifyProviderAsync(Guid providerId, bool isVerified, CancellationToken ct = default);

    /// <summary>Sets IsAvailable flag for the given provider.</summary>
    /// <exception cref="KeyNotFoundException">Profile not found.</exception>
    Task<ProviderProfileDto> SetAvailabilityAsync(Guid providerId, bool isAvailable, CancellationToken ct = default);

    // ── Internal (called by review-service) ──────────────────────────────────

    /// <summary>Updates the aggregated average rating (called internally when a review is added/removed).</summary>
    /// <exception cref="KeyNotFoundException">Profile not found.</exception>
    Task UpdateRatingAsync(Guid providerId, double newAvgRating, CancellationToken ct = default);
}
