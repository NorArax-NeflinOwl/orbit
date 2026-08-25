namespace Orbit.Core.Notifications;

public interface INotificationEntryRepository
{
    Task AddAsync(NotificationEntry entry, CancellationToken cancellationToken);

    /// <summary>Most recent first, capped at take rows.</summary>
    Task<IReadOnlyList<NotificationEntry>> GetRecentAsync(Guid userId, int take, CancellationToken cancellationToken);

    /// <summary>
    /// The unread entries themselves, most recent first, capped at take rows - the client needs these
    /// rather than a bare count so it can badge the individual places a notification came from (a chat
    /// contact, the Tasks nav item, ...) from their Url, not just the avatar.
    /// </summary>
    Task<IReadOnlyList<NotificationEntry>> GetUnreadAsync(Guid userId, int take, CancellationToken cancellationToken);

    /// <summary>Marks every currently-unread entry for userId as read as of nowUtc.</summary>
    Task MarkAllReadAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>Removes every entry for userId - what the panel's "Clear" action does, as opposed to merely marking them read.</summary>
    Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken);
}
