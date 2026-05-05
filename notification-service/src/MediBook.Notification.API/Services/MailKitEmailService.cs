using MediBook.Notification.API.Helpers;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MediBook.Notification.API.Services;

/// <summary>
/// SMTP email sender powered by MailKit + MimeKit.
///
/// MailKit is the most widely-used SMTP library for .NET — it is the direct
/// .NET equivalent of Node.js Nodemailer, supporting STARTTLS, SSL, OAuth2,
/// and all major SMTP providers (Gmail, Outlook, Mailtrap, SendGrid, etc.).
///
/// Configuration (appsettings.json → "EmailSettings"):
///   SmtpHost, SmtpPort, UseSsl, Username, Password, FromEmail, FromName
/// </summary>
public sealed class MailKitEmailService : IEmailService
{
    private readonly EmailSettings                   _settings;
    private readonly ILogger<MailKitEmailService>    _logger;

    public MailKitEmailService(
        IOptions<EmailSettings>            settings,
        ILogger<MailKitEmailService>       logger)
    {
        _settings = settings.Value;
        _logger   = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        var message = BuildMessage(toEmail, toName, subject, htmlBody);
        await SendMessageAsync(message, ct);
    }

    /// <inheritdoc />
    public async Task SendBulkAsync(
        IEnumerable<(string Email, string Name)> recipients,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        // Send one email per recipient so each TO: header is personalised.
        // For high volume, replace with a BCC batch or transactional provider.
        var tasks = recipients.Select(r => SendAsync(r.Email, r.Name, subject, htmlBody, ct));
        await Task.WhenAll(tasks);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private MimeMessage BuildMessage(
        string toEmail,
        string toName,
        string subject,
        string htmlBody)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;

        // BodyBuilder allows setting both HTML and a plain-text fallback
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody  = htmlBody,
            TextBody  = HtmlToPlainText(htmlBody)
        };

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private async Task SendMessageAsync(MimeMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient();

        try
        {
            // SecureSocketOptions.Auto → picks STARTTLS on 587, SSL on 465
            var socketOptions = _settings.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOptions, ct);

            // Authenticate only when credentials are provided
            if (!string.IsNullOrWhiteSpace(_settings.Username))
                await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);

            await client.SendAsync(message, ct);

            _logger.LogInformation(
                "Email sent to {To} | Subject: {Subject}",
                message.To.ToString(),
                message.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email to {To} | Subject: {Subject}",
                message.To.ToString(),
                message.Subject);
            throw;
        }
        finally
        {
            await client.DisconnectAsync(quit: true, ct);
        }
    }

    /// <summary>
    /// Naive HTML → plain-text strip for the fallback text part.
    /// For production consider HtmlAgilityPack for proper stripping.
    /// </summary>
    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        // Remove tags, collapse whitespace
        var plain = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        plain = System.Text.RegularExpressions.Regex.Replace(plain, @"\s+", " ").Trim();
        return plain;
    }
}
