namespace MediBook.Notification.API.Helpers;

/// <summary>Strongly-typed binding for the "EmailSettings" config section.</summary>
public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    /// <summary>SMTP host, e.g. "smtp.gmail.com" or "smtp.mailtrap.io".</summary>
    public string SmtpHost     { get; init; } = string.Empty;

    /// <summary>SMTP port, e.g. 587 (STARTTLS) or 465 (SSL).</summary>
    public int    SmtpPort     { get; init; } = 587;

    /// <summary>Whether to use SSL/TLS on connection (port 465). False = STARTTLS on 587.</summary>
    public bool   UseSsl       { get; init; } = false;

    /// <summary>SMTP login username.</summary>
    public string Username     { get; init; } = string.Empty;

    /// <summary>SMTP login password or app password.</summary>
    public string Password     { get; init; } = string.Empty;

    /// <summary>The "From" email address shown to recipients.</summary>
    public string FromEmail    { get; init; } = string.Empty;

    /// <summary>The "From" display name shown to recipients.</summary>
    public string FromName     { get; init; } = "MediBook";
}
