namespace MediBook.Notification.API.DTOs;

// ── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>Payload for sending a single notification (in-app + email).</summary>
public sealed record SendNotificationRequest(
    Guid    RecipientId,
    string  RecipientEmail,
    string  RecipientName,
    string  Type,
    string  Title,
    string  Message,
    string  Channel,
    Guid?   RelatedId   = null,
    string? RelatedType = null
);

/// <summary>Payload for sending a bulk notification to many recipients.</summary>
public sealed record SendBulkRequest(
    List<Guid>    RecipientIds,
    List<string>  RecipientEmails,
    List<string>  RecipientNames,
    string        Type,
    string        Title,
    string        Message,
    string        Channel
);

/// <summary>Payload for sending a plain email (no persisted notification record).</summary>
public sealed record SendEmailRequest(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody
);

// ── Response DTOs ─────────────────────────────────────────────────────────────

/// <summary>Represents a notification returned to the client.</summary>
public sealed record NotificationResponse(
    Guid     Id,
    Guid     RecipientId,
    string   Type,
    string   Title,
    string   Message,
    string   Channel,
    Guid?    RelatedId,
    string?  RelatedType,
    bool     IsRead,
    DateTime SentAt,
    DateTime CreatedAt
);

public sealed record UnreadCountResponse(int Count);

public sealed record BulkSendResponse(int SentCount, List<string> Errors);

/// <summary>Generic error wrapper returned on 4xx / 5xx responses.</summary>
public sealed record ApiErrorResponse(
    string Message,
    IEnumerable<string>? Errors = null
);

/// <summary>Generic success wrapper.</summary>
public sealed record ApiSuccessResponse(string Message);
