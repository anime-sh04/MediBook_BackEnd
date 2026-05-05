using MediBook.Notification.API.DTOs;
using MediBook.Notification.API.Entities;
using MediBook.Notification.API.Hubs;
using MediBook.Notification.API.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace MediBook.Notification.API.Services;

/// <summary>
/// Orchestrates three notification channels:
///   1. Persistence — saves the notification record to PostgreSQL.
///   2. Real-time   — pushes the payload to connected clients via SignalR.
///   3. Email       — sends an HTML email via MailKit (Nodemailer equivalent).
///
/// SMS dispatch is intentionally omitted per project requirements.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository         _repo;
    private readonly IEmailService                   _emailService;
    private readonly IHubContext<NotificationHub>    _hub;
    private readonly ILogger<NotificationService>    _logger;

    public NotificationService(
        INotificationRepository      repo,
        IEmailService                emailService,
        IHubContext<NotificationHub> hub,
        ILogger<NotificationService> logger)
    {
        _repo         = repo;
        _emailService = emailService;
        _hub          = hub;
        _logger       = logger;
    }

    // ── Single notification ───────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<NotificationResponse> SendAsync(
        SendNotificationRequest request,
        CancellationToken ct = default)
    {
        // 1. Persist
        var notification = Entities.Notification.Create(
            recipientId : request.RecipientId,
            type        : request.Type,
            title       : request.Title,
            message     : request.Message,
            channel     : request.Channel,
            relatedId   : request.RelatedId,
            relatedType : request.RelatedType);

        await _repo.AddAsync(notification, ct);
        await _repo.SaveChangesAsync(ct);

        // 2. Real-time push (fire-and-forget on failure — don't block the caller)
        if (request.Channel is NotificationChannels.App or NotificationChannels.Email)
        {
            _ = PushSignalRAsync(request.RecipientId, notification);
        }

        // 3. Email dispatch
        if (request.Channel == NotificationChannels.Email
            && !string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            _ = SendEmailSafeAsync(
                request.RecipientEmail,
                request.RecipientName,
                request.Title,
                BuildHtmlBody(request.Title, request.Message),
                ct);
        }

        _logger.LogInformation(
            "Notification sent | Type={Type} | Channel={Channel} | RecipientId={RecipientId}",
            notification.Type, notification.Channel, notification.RecipientId);

        return ToResponse(notification);
    }

    // ── Bulk ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<BulkSendResponse> SendBulkAsync(
        SendBulkRequest request,
        CancellationToken ct = default)
    {
        if (request.RecipientIds.Count == 0)
            return new BulkSendResponse(0, new List<string>());

        var notifications = new List<Entities.Notification>(request.RecipientIds.Count);
        var errors        = new List<string>();
        int sentCount     = 0;

        for (int i = 0; i < request.RecipientIds.Count; i++)
        {
            var recipientId    = request.RecipientIds[i];
            var recipientEmail = i < request.RecipientEmails.Count ? request.RecipientEmails[i] : string.Empty;
            var recipientName  = i < request.RecipientNames.Count  ? request.RecipientNames[i]  : string.Empty;

            var notification = Entities.Notification.Create(
                recipientId : recipientId,
                type        : request.Type,
                title       : request.Title,
                message     : request.Message,
                channel     : request.Channel);

            notifications.Add(notification);

            // Real-time push
            _ = PushSignalRAsync(recipientId, notification);

            // Email
            if (request.Channel == NotificationChannels.Email
                && !string.IsNullOrWhiteSpace(recipientEmail))
            {
                try
                {
                    await _emailService.SendAsync(
                        recipientEmail,
                        recipientName,
                        request.Title,
                        BuildHtmlBody(request.Title, request.Message),
                        ct);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Bulk email failed for {Email}", recipientEmail);
                    errors.Add($"Email failed for {recipientEmail}: {ex.Message}");
                }
            }
            else
            {
                sentCount++;
            }
        }

        await _repo.AddRangeAsync(notifications, ct);
        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Bulk notification sent | Type={Type} | Recipients={Count}",
            request.Type, request.RecipientIds.Count);

        return new BulkSendResponse(sentCount, errors);
    }

    // ── Retrieval ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<NotificationResponse>> GetByRecipientAsync(
        Guid recipientId,
        int  page     = 1,
        int  pageSize = 20,
        CancellationToken ct = default)
    {
        var list = await _repo.GetByRecipientIdAsync(recipientId, page, pageSize, ct);
        return list.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<UnreadCountResponse> GetUnreadCountAsync(
        Guid recipientId,
        CancellationToken ct = default)
    {
        var count = await _repo.CountByRecipientIdAndIsReadAsync(recipientId, isRead: false, ct);
        return new UnreadCountResponse(count);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NotificationResponse>> GetAllAsync(
        int page     = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var list = await _repo.GetAllAsync(page, pageSize, ct);
        return list.Select(ToResponse).ToList();
    }

    // ── Read-state ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> MarkAsReadAsync(Guid id, CancellationToken ct = default)
    {
        var result = await _repo.MarkAsReadAsync(id, ct);
        if (result) await _repo.SaveChangesAsync(ct);
        return result;
    }

    /// <inheritdoc />
    public async Task MarkAllReadAsync(Guid recipientId, CancellationToken ct = default)
    {
        await _repo.MarkAllReadAsync(recipientId, ct);
        // ExecuteUpdateAsync already saves, no SaveChangesAsync needed
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return await _repo.DeleteByIdAsync(id, ct);
        // ExecuteDeleteAsync already saves
    }

    // ── Direct email ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task SendEmailAsync(SendEmailRequest request, CancellationToken ct = default)
    {
        await _emailService.SendAsync(
            request.ToEmail,
            request.ToName,
            request.Subject,
            request.HtmlBody,
            ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task PushSignalRAsync(
        Guid                      recipientId,
        Entities.Notification     notification)
    {
        try
        {
            var payload = ToResponse(notification);
            // Send to the group named after the userId (set up in NotificationHub.OnConnectedAsync)
            await _hub.Clients
                      .Group(recipientId.ToString())
                      .SendAsync("ReceiveNotification", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SignalR push failed for RecipientId={RecipientId}", recipientId);
        }
    }

    private async Task SendEmailSafeAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken ct)
    {
        try
        {
            await _emailService.SendAsync(toEmail, toName, subject, htmlBody, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Email dispatch failed | To={Email} | Subject={Subject}",
                toEmail, subject);
        }
    }

    /// <summary>
    /// Generates a simple, branded HTML email body.
    /// Replace with a Razor-rendered template for richer emails.
    /// </summary>
    private static string BuildHtmlBody(string title, string message)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
              <title>{title}</title>
            </head>
            <body style="margin:0;padding:0;background-color:#f4f7f9;font-family:Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f7f9;padding:40px 0;">
                <tr>
                  <td align="center">
                    <table width="600" cellpadding="0" cellspacing="0"
                           style="background:#ffffff;border-radius:8px;
                                  box-shadow:0 2px 8px rgba(0,0,0,.08);
                                  overflow:hidden;">
                      <!-- Header -->
                      <tr>
                        <td style="background:#1a5f7a;padding:28px 32px;">
                          <h1 style="margin:0;color:#ffffff;font-size:22px;
                                     font-weight:700;letter-spacing:.5px;">
                            MediBook
                          </h1>
                          <p style="margin:4px 0 0;color:#a8d8ea;font-size:13px;">
                            Book Smarter. Heal Faster. Care Better.
                          </p>
                        </td>
                      </tr>
                      <!-- Body -->
                      <tr>
                        <td style="padding:32px;">
                          <h2 style="margin:0 0 16px;color:#1a5f7a;font-size:18px;">{title}</h2>
                          <p style="margin:0 0 24px;color:#444444;font-size:15px;
                                    line-height:1.6;">{message}</p>
                          <hr style="border:none;border-top:1px solid #e8ecef;margin:24px 0;"/>
                          <p style="margin:0;color:#888888;font-size:12px;">
                            This is an automated message from MediBook. Please do not reply to this email.
                          </p>
                        </td>
                      </tr>
                      <!-- Footer -->
                      <tr>
                        <td style="background:#f0f5f8;padding:16px 32px;
                                   text-align:center;color:#999999;font-size:11px;">
                          &copy; {DateTime.UtcNow.Year} MediBook Platform. All rights reserved.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static NotificationResponse ToResponse(Entities.Notification n) =>
        new(n.Id, n.RecipientId, n.Type, n.Title, n.Message,
            n.Channel, n.RelatedId, n.RelatedType, n.IsRead, n.SentAt, n.CreatedAt);
}
