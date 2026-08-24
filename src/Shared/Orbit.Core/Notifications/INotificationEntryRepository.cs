namespace Orbit.Core.Notifications;

public interface INotificationEntryRepository
{
    Task AddAsync(NotificationEntry entry, CancellationToken cancellationToken);

    /// <summary>Most recent first, capped at take rows.</summary>
    Task<IReadOnlyList<NotificationEntry>> GetRecentAsync(Guid userId, int take, CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Marks every currently-unread entry for userId as read as of nowUtc.</summary>
    Task MarkAllReadAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken);
}
