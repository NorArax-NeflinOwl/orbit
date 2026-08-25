namespace Orbit.Core.Notifications;

/// <summary>
/// One row in a user's in-app notification feed - recorded alongside (not instead of) an actual push/
/// email send, so the feed/unread badge/toast banner have something to read regardless of whether the
/// user has push or email delivery enabled. Reuses the same Title/Body/Url a background service already
/// built for its push send (see PushNotificationPayload) rather than reformatting the content a second
/// time.
/// </summary>
public sealed class NotificationEntry
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public NotificationEntryKind Kind { get; private set; }
    public string Title { get; private set; }
    public string Body { get; private set; }

    /// <summary>Where clicking this entry should navigate to (e.g. the event, the task list, the conversation) - null if there's nowhere more specific to go.</summary>
    public string? Url { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }
    public bool IsRead => ReadAtUtc is not null;

    /// <summary>
    /// When the reader cleared this entry out of the feed. Dismissing hides it from the panel without
    /// destroying it: the notifications page still shows it, until the reader's own retention window
    /// deletes it for good (see NotificationSettings.RetentionDays). "Clear" used to delete on the
    /// spot, which meant a notification glanced at and cleared was unrecoverable.
    /// </summary>
    public DateTimeOffset? DismissedAtUtc { get; private set; }

    public bool IsDismissed => DismissedAtUtc is not null;

    private NotificationEntry(
        Guid id, Guid userId, NotificationEntryKind kind, string title, string body, string? url,
        DateTimeOffset createdAtUtc, DateTimeOffset? readAtUtc, DateTimeOffset? dismissedAtUtc)
    {
        Id = id;
        UserId = userId;
        Kind = kind;
        Title = title;
        Body = body;
        Url = url;
        CreatedAtUtc = createdAtUtc;
        ReadAtUtc = readAtUtc;
        DismissedAtUtc = dismissedAtUtc;
    }

    public static NotificationEntry Create(Guid userId, NotificationEntryKind kind, string title, string body, string? url)
        => new(Guid.NewGuid(), userId, kind, title, body, url, DateTimeOffset.UtcNow, readAtUtc: null, dismissedAtUtc: null);

    public static NotificationEntry FromPersistence(
        Guid id, Guid userId, NotificationEntryKind kind, string title, string body, string? url,
        DateTimeOffset createdAtUtc, DateTimeOffset? readAtUtc, DateTimeOffset? dismissedAtUtc = null)
        => new(id, userId, kind, title, body, url, createdAtUtc, readAtUtc, dismissedAtUtc);

    /// <summary>Idempotent, mirroring NoteShare.MarkAccepted - marking an already-read entry read again is a no-op, not a timestamp bump.</summary>
    public void MarkRead(DateTimeOffset nowUtc) => ReadAtUtc ??= nowUtc;

    /// <summary>
    /// Clearing an entry out of the panel. Idempotent like MarkRead, and implies read: an entry the
    /// reader has dismissed is one they have finished with, so leaving it counted as unread would keep
    /// the badge lit over a panel showing nothing.
    /// </summary>
    public void Dismiss(DateTimeOffset nowUtc)
    {
        DismissedAtUtc ??= nowUtc;
        MarkRead(nowUtc);
    }
}
