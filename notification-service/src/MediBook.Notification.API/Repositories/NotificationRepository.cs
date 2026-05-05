using MediBook.Notification.API.Data;
using Microsoft.EntityFrameworkCore;

namespace MediBook.Notification.API.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _db;

    public NotificationRepository(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task<Entities.Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Notifications.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<Entities.Notification>> GetByRecipientIdAsync(
        Guid recipientId,
        int  page     = 1,
        int  pageSize = 20,
        CancellationToken ct = default)
    {
        return await _db.Notifications
            .Where(n => n.RecipientId == recipientId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Entities.Notification>> GetByRecipientIdAndIsReadAsync(
        Guid recipientId,
        bool isRead,
        CancellationToken ct = default)
    {
        return await _db.Notifications
            .Where(n => n.RecipientId == recipientId && n.IsRead == isRead)
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<int> CountByRecipientIdAndIsReadAsync(
        Guid recipientId,
        bool isRead,
        CancellationToken ct = default)
    {
        return await _db.Notifications
            .CountAsync(n => n.RecipientId == recipientId && n.IsRead == isRead, ct);
    }

    public async Task<IReadOnlyList<Entities.Notification>> GetByTypeAsync(
        string type,
        CancellationToken ct = default)
    {
        return await _db.Notifications
            .Where(n => n.Type == type.ToUpperInvariant())
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Entities.Notification>> GetByRelatedIdAsync(
        Guid relatedId,
        CancellationToken ct = default)
    {
        return await _db.Notifications
            .Where(n => n.RelatedId == relatedId)
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Entities.Notification>> GetAllAsync(
        int page     = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        return await _db.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task AddAsync(Entities.Notification notification, CancellationToken ct = default)
    {
        await _db.Notifications.AddAsync(notification, ct);
    }

    public async Task AddRangeAsync(IEnumerable<Entities.Notification> notifications, CancellationToken ct = default)
    {
        await _db.Notifications.AddRangeAsync(notifications, ct);
    }

    public async Task<bool> MarkAsReadAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _db.Notifications.FindAsync(new object[] { id }, ct);
        if (notification is null) return false;
        notification.MarkAsRead();
        return true;
    }

    public async Task MarkAllReadAsync(Guid recipientId, CancellationToken ct = default)
    {
        await _db.Notifications
            .Where(n => n.RecipientId == recipientId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }

    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken ct = default)
    {
        var deleted = await _db.Notifications
            .Where(n => n.Id == id)
            .ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
