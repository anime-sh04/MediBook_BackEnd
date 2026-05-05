namespace MediBook.Notification.API.Repositories;

public interface INotificationRepository
{
    Task<Entities.Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Entities.Notification>> GetByRecipientIdAsync(
        Guid recipientId,
        int  page     = 1,
        int  pageSize = 20,
        CancellationToken ct = default);

    Task<IReadOnlyList<Entities.Notification>> GetByRecipientIdAndIsReadAsync(
        Guid recipientId,
        bool isRead,
        CancellationToken ct = default);

    Task<int> CountByRecipientIdAndIsReadAsync(
        Guid recipientId,
        bool isRead,
        CancellationToken ct = default);

    Task<IReadOnlyList<Entities.Notification>> GetByTypeAsync(
        string type,
        CancellationToken ct = default);

    Task<IReadOnlyList<Entities.Notification>> GetByRelatedIdAsync(
        Guid relatedId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Entities.Notification>> GetAllAsync(
        int  page     = 1,
        int  pageSize = 50,
        CancellationToken ct = default);

    Task AddAsync(Entities.Notification notification, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Entities.Notification> notifications, CancellationToken ct = default);

    Task<bool> MarkAsReadAsync(Guid id, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid recipientId, CancellationToken ct = default);

    Task<bool> DeleteByIdAsync(Guid id, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
