namespace Orbit.Core.Notifications;

public interface INotificationEntryRepository
{
    Task AddAsync(NotificationEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// What the panel shows: most recent first, capped at take rows, and excluding anything the reader
    /// has already cleared away - see GetHistoryAsync for the view that keeps those.
    /// </summary>
    Task<IReadOnlyList<NotificationEntry>> GetRecentAsync(Guid userId, int take, CancellationToken cancellationToken);

    /// <summary>
    /// Everything still held for userId, dismissed entries included, most recent first. This is the
    /// notifications page's own view: clearing the panel is not meant to destroy the record, only to
    /// tidy it out of the way until the retention window expires it.
    /// </summary>
    Task<IReadOnlyList<NotificationEntry>> GetHistoryAsync(Guid userId, int take, CancellationToken cancellationToken);

    /// <summary>
    /// The unread entries themselves, most recent first, capped at take rows - the client needs these
    /// rather than a bare count so it can badge the individual places a notification came from (a chat
    /// contact, the Tasks nav item, ...) from their Url, not just the avatar.
    /// </summary>
    Task<IReadOnlyList<NotificationEntry>> GetUnreadAsync(Guid userId, int take, CancellationToken cancellationToken);

    /// <summary>Marks every currently-unread entry for userId as read as of nowUtc.</summary>
    Task MarkAllReadAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Marks read every unread entry for userId that points at url. Arriving at the page a notification
    /// was about is the same as having read it, so the badge clears whether the reader got there
    /// through the panel or by walking to the page themselves.
    /// </summary>
    Task MarkReadByUrlAsync(Guid userId, string url, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>Clears every entry for userId out of the panel - what "Clear" does, keeping them readable on the notifications page.</summary>
    Task DismissAllAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes entries that have outlived the retention window their own reader chose, as of nowUtc.
    /// defaultRetention applies to a user who has never saved notification settings. Returns how many
    /// rows went, so the sweep can say whether it did anything.
    /// </summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset nowUtc, TimeSpan defaultRetention, CancellationToken cancellationToken);
}
