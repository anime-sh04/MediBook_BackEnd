namespace MediBook.Notification.API.Entities;

/// <summary>
/// Represents a single notification sent to a platform user.
/// Supports in-app (APP), email (EMAIL), and SMS channels.
/// NOTE: SMS channel is defined in the enum for schema completeness
///       but the current implementation only dispatches APP and EMAIL.
/// </summary>
public class Notification
{
    public Guid     Id            { get; private set; }
    public Guid     RecipientId   { get; private set; }

    /// <summary>BOOKING | REMINDER | CANCELLATION | PAYMENT | FOLLOWUP</summary>
    public string   Type          { get; private set; } = string.Empty;

    public string   Title         { get; private set; } = string.Empty;
    public string   Message       { get; private set; } = string.Empty;

    /// <summary>APP | EMAIL | SMS</summary>
    public string   Channel       { get; private set; } = string.Empty;

    /// <summary>ID of the related domain object (e.g. AppointmentId, PaymentId).</summary>
    public Guid?    RelatedId     { get; private set; }

    /// <summary>Domain type of the related object (e.g. "Appointment", "Payment").</summary>
    public string?  RelatedType   { get; private set; }

    public bool     IsRead        { get; private set; }
    public DateTime SentAt        { get; private set; }
    public DateTime CreatedAt     { get; private set; }

    // EF Core parameterless constructor
    private Notification() { }

    // ── Factory ──────────────────────────────────────────────────────────────

    public static Notification Create(
        Guid   recipientId,
        string type,
        string title,
        string message,
        string channel,
        Guid?  relatedId   = null,
        string? relatedType = null)
    {
        if (recipientId == Guid.Empty)
            throw new ArgumentException("RecipientId is required.", nameof(recipientId));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type is required.", nameof(type));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("Channel is required.", nameof(channel));

        return new Notification
        {
            Id          = Guid.NewGuid(),
            RecipientId = recipientId,
            Type        = type.ToUpperInvariant(),
            Title       = title.Trim(),
            Message     = message.Trim(),
            Channel     = channel.ToUpperInvariant(),
            RelatedId   = relatedId,
            RelatedType = relatedType?.Trim(),
            IsRead      = false,
            SentAt      = DateTime.UtcNow,
            CreatedAt   = DateTime.UtcNow
        };
    }

    // ── Mutators ──────────────────────────────────────────────────────────────

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
