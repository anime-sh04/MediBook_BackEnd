namespace MediBook.Review.API.Entities;

/// <summary>
/// Represents a patient's review of a provider after a completed appointment.
/// One review is permitted per appointment (unique constraint on AppointmentId).
/// Rating is constrained to 1–5.
/// </summary>
public sealed class Review
{
    // ── Properties ────────────────────────────────────────────────────────────
    public int      ReviewId      { get; private set; }
    public int      AppointmentId { get; private set; }  // unique — one review per appointment
    public Guid     PatientId     { get; private set; }
    public Guid     ProviderId    { get; private set; }
    public int      Rating        { get; private set; }  // 1–5
    public string   Comment       { get; private set; } = string.Empty;
    public DateOnly ReviewDate    { get; private set; }
    public bool     IsVerified    { get; private set; }
    public bool     IsAnonymous   { get; private set; }

    private Review() { } // EF Core

    // ── Factory ───────────────────────────────────────────────────────────────

    public static Review Create(
        int    appointmentId,
        Guid   patientId,
        Guid   providerId,
        int    rating,
        string comment,
        bool   isAnonymous = false)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5.");
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("Comment is required.");

        return new Review
        {
            AppointmentId = appointmentId,
            PatientId     = patientId,
            ProviderId    = providerId,
            Rating        = rating,
            Comment       = comment.Trim(),
            ReviewDate    = DateOnly.FromDateTime(DateTime.UtcNow),
            IsVerified    = false,
            IsAnonymous   = isAnonymous
        };
    }

    // ── State mutations ───────────────────────────────────────────────────────

    /// <summary>Updates rating and comment (e.g. patient edits their review).</summary>
    public void Update(int rating, string comment)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5.");
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("Comment is required.");

        Rating    = rating;
        Comment   = comment.Trim();
    }

    /// <summary>Marks the review as admin-verified (not fraudulent).</summary>
    public void Verify() => IsVerified = true;

    // ── Read helpers (match class diagram) ────────────────────────────────────
    public int  GetReviewId()  => ReviewId;
    public int  GetRating()    => Rating;
    public bool GetIsAnonymous() => IsAnonymous;
}
