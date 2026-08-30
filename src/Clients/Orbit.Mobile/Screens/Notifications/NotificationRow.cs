using Orbit.Contracts.Notifications;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Notifications;

namespace Orbit.Mobile.Screens.Notifications;

/// <summary>
/// One notification as the feed shows it. Wraps the server's DTO rather than binding to it directly so
/// the row can say what the screen needs to know - chiefly whether tapping it will lead anywhere, which
/// the DTO expresses only as a path this build may or may not recognise.
/// </summary>
/// <param name="Translations">
/// What says it in the reader's language. The server sends the English sentence and what fills its
/// holes rather than a finished one, because it has no idea what language this phone is set to - see
/// Orbit.Core's PushNotificationPayload. Before that, every notification read in English here whatever
/// the rest of the screen was written in.
/// </param>
public sealed record NotificationRow(NotificationEntryDto Entry, Translations Translations)
{
    public Guid Id => Entry.Id;

    public string Title => Say(Entry.Title, Entry.TitleArguments);

    public string Body => Say(Entry.Body, Entry.BodyArguments);

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

    /// <summary>
    /// The sentence in the reader's language, with the values put back in. An entry with no arguments
    /// is looked up whole, which is what a phrase like "New message" is and what every entry written
    /// before the server split them apart looks like.
    /// </summary>
    private string Say(string format, IReadOnlyList<string>? arguments)
        => arguments is { Count: > 0 }
            ? Translations.Format(format, [.. arguments])
            : Translations[format];
}
