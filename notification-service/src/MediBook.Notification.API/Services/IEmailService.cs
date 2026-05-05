namespace MediBook.Notification.API.Services;

/// <summary>
/// Sends transactional emails via SMTP using MailKit.
/// This is the .NET equivalent of Node's Nodemailer — MailKit is the
/// de-facto standard SMTP client for .NET Core.
/// </summary>
public interface IEmailService
{
    /// <summary>Send a single HTML email.</summary>
    Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken ct = default);

    /// <summary>Send the same email to many recipients.</summary>
    Task SendBulkAsync(
        IEnumerable<(string Email, string Name)> recipients,
        string subject,
        string htmlBody,
        CancellationToken ct = default);
}
