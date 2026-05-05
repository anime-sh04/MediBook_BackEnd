namespace MediBook.Review.API.DTOs;

// ── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>Payload to submit a new review for a completed appointment.</summary>
public sealed record AddReviewRequest(
    int    AppointmentId,
    Guid   PatientId,
    Guid   ProviderId,
    int    Rating,        // 1–5
    string Comment,
    bool   IsAnonymous = false
);

/// <summary>Payload to update an existing review's rating and comment.</summary>
public sealed record UpdateReviewRequest(
    int    Rating,
    string Comment
);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public sealed record ReviewDto(
    int      ReviewId,
    int      AppointmentId,
    Guid?    PatientId,     // null when IsAnonymous = true and caller is not admin
    Guid     ProviderId,
    int      Rating,
    string   Comment,
    string   ReviewDate,   // "yyyy-MM-dd"
    bool     IsVerified,
    bool     IsAnonymous
);

public sealed record AvgRatingDto(
    Guid   ProviderId,
    double AvgRating,
    int    ReviewCount
);

// ── Shared ─────────────────────────────────────────────────────────────────────

public sealed record ApiErrorResponse(
    string               Message,
    IEnumerable<string>? Errors = null
);
