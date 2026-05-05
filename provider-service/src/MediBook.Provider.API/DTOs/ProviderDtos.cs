namespace MediBook.Provider.API.DTOs;

// ── Registration / Profile Update ────────────────────────────────────────────

public sealed record RegisterProviderRequest(
    string  Specialization,
    string  Qualification,
    int     ExperienceYears,
    string  Bio,
    string  ClinicName,
    string  ClinicAddress,
    string  City,
    string  State,
    decimal ConsultationFee
);

// ── Search / Filter ───────────────────────────────────────────────────────────

/// <summary>Query parameters for GET /api/v1/providers</summary>
public sealed record ProviderSearchQuery(
    string?  Specialization = null,
    string?  Name           = null,
    string?  City           = null,
    bool?    IsVerified      = null,
    bool?    IsAvailable     = null,
    int      Page            = 1,
    int      PageSize        = 20
);

// ── Admin Actions ─────────────────────────────────────────────────────────────

/// <summary>Payload for PUT /api/v1/providers/{id}/availability</summary>
public sealed record SetAvailabilityRequest(bool IsAvailable);

/// <summary>Payload for PUT /api/v1/providers/{id}/rating (internal — called by review-service)</summary>
public sealed record UpdateRatingRequest(double NewAvgRating);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public sealed record ProviderProfileDto(
    Guid     ProviderId,
    Guid     UserId,
    string   Specialization,
    string   Qualification,
    int      ExperienceYears,
    string   Bio,
    string   ClinicName,
    string   ClinicAddress,
    string   City,
    string   State,
    decimal  ConsultationFee,
    bool     IsVerified,
    bool     IsAvailable,
    double   AvgRating,
    DateTime CreatedAt
);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int              TotalCount,
    int              Page,
    int              PageSize
);

// ── Shared Error Envelope ────────────────────────────────────────────────────

public sealed record ApiErrorResponse(
    string                Message,
    IEnumerable<string>?  Errors = null
);
