using Orbit.Contracts.Notifications;
using Orbit.Mobile.Notifications;

namespace Orbit.Mobile.Screens.Notifications;

/// <summary>
/// One notification as the feed shows it. Wraps the server's DTO rather than binding to it directly so
/// the row can say what the screen needs to know - chiefly whether tapping it will lead anywhere, which
/// the DTO expresses only as a path this build may or may not recognise.
/// </summary>
public sealed record NotificationRow(NotificationEntryDto Entry)
{
    public Guid Id => Entry.Id;

    public string Title => Entry.Title;

    public string Body => Entry.Body;

    public DateTimeOffset CreatedAtUtc => Entry.CreatedAtUtc;

    public bool IsRead => Entry.IsRead;

    /// <summary>Unread entries are the ones worth drawing attention to, so the row states it positively.</summary>
    public bool IsUnread => !Entry.IsRead;

    public string? Url => Entry.Url;

    /// <summary>
    /// Whether a tap has somewhere to go. False both for a notification that names no destination and
    /// for one naming a path this build does not know - the row still reads either way.
    /// </summary>
    public bool CanBeOpened => NotificationDestination.Parse(Entry.Url) is not null;
}
