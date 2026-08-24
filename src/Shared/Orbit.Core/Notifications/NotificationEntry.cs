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

    private NotificationEntry(
        Guid id, Guid userId, NotificationEntryKind kind, string title, string body, string? url,
        DateTimeOffset createdAtUtc, DateTimeOffset? readAtUtc)
    {
        Id = id;
        UserId = userId;
        Kind = kind;
        Title = title;
        Body = body;
        Url = url;
        CreatedAtUtc = createdAtUtc;
        ReadAtUtc = readAtUtc;
    }

    public static NotificationEntry Create(Guid userId, NotificationEntryKind kind, string title, string body, string? url)
        => new(Guid.NewGuid(), userId, kind, title, body, url, DateTimeOffset.UtcNow, readAtUtc: null);

    public static NotificationEntry FromPersistence(
        Guid id, Guid userId, NotificationEntryKind kind, string title, string body, string? url,
        DateTimeOffset createdAtUtc, DateTimeOffset? readAtUtc)
        => new(id, userId, kind, title, body, url, createdAtUtc, readAtUtc);

    /// <summary>Idempotent, mirroring NoteShare.MarkAccepted - marking an already-read entry read again is a no-op, not a timestamp bump.</summary>
    public void MarkRead(DateTimeOffset nowUtc) => ReadAtUtc ??= nowUtc;
}
