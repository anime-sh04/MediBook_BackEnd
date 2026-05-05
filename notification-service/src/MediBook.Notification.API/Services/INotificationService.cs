using MediBook.Notification.API.DTOs;

namespace MediBook.Notification.API.Services;

/// <summary>
/// Core notification service contract.
/// Orchestrates persistence + real-time push + email dispatch.
/// </summary>
public interface INotificationService
{
    // ── Single notification ───────────────────────────────────────────────────

    /// <summary>
    /// Persist a notification, push it via SignalR, and optionally send an email.
    /// </summary>
    Task<NotificationResponse> SendAsync(
        SendNotificationRequest request,
        CancellationToken ct = default);

    // ── Bulk ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Send the same notification to a list of recipients (Admin broadcast).
    /// </summary>
    Task<BulkSendResponse> SendBulkAsync(
        SendBulkRequest request,
        CancellationToken ct = default);

    // ── Retrieval ─────────────────────────────────────────────────────────────

    Task<IReadOnlyList<NotificationResponse>> GetByRecipientAsync(
        Guid recipientId,
        int  page     = 1,
        int  pageSize = 20,
        CancellationToken ct = default);

    Task<UnreadCountResponse> GetUnreadCountAsync(
        Guid recipientId,
        CancellationToken ct = default);

    Task<IReadOnlyList<NotificationResponse>> GetAllAsync(
        int page     = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    // ── Read-state management ─────────────────────────────────────────────────

    Task<bool> MarkAsReadAsync(Guid notificationId, CancellationToken ct = default);
    Task       MarkAllReadAsync(Guid recipientId,   CancellationToken ct = default);

    // ── Delete ────────────────────────────────────────────────────────────────

    Task<bool> DeleteAsync(Guid notificationId, CancellationToken ct = default);

    // ── Direct email (no persisted record) ───────────────────────────────────

    Task SendEmailAsync(
        SendEmailRequest request,
        CancellationToken ct = default);
}
