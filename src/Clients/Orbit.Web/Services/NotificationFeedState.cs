using Orbit.Contracts.Notifications;

namespace Orbit.Web.Services;

/// <summary>
/// The currently-unread notification entries, shared by everything that badges them: MainLayout's avatar
/// and nav items, and Chat's contact list. MainLayout owns the polling and calls <see cref="Set"/>;
/// everyone else subscribes to <see cref="Changed"/> and reads the counts below, so a badge never needs
/// its own poll. Mirrors ThemeService's shape - scoped state plus a Changed event.
/// </summary>
public sealed class NotificationFeedState
{
    private IReadOnlyList<NotificationEntryDto> _unreadEntries = [];

    /// <summary>Raised whenever the unread set changes, so subscribed components can re-render their badges.</summary>
    public event Action? Changed;

    /// <summary>Capped by the server (see NotificationEndpoints' MaxRecentEntries), which is well past the point the badge just reads "9+".</summary>
    public int UnreadCount => _unreadEntries.Count;

    public void Set(IReadOnlyList<NotificationEntryDto> unreadEntries)
    {
        _unreadEntries = unreadEntries;
        Changed?.Invoke();
    }

    public void Clear() => Set([]);

    /// <summary>
    /// How many unread entries point somewhere under urlPrefix - what badges a nav section, since a
    /// notification's Url is the in-app page it came from ("/tasks/{id}", "/calendar/{id}", ...).
    /// </summary>
    public int CountForSection(string urlPrefix)
        => _unreadEntries.Count(entry => entry.Url is { } url && url.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Unread messages from one specific chat partner, for the avatar badges in Chat's contact list.</summary>
    public int CountForChatWith(Guid otherUserId) => CountForSection($"/chat/{otherUserId}");

    /// <summary>
    /// Whether anything unread points at exactly this page - checked before asking the server to mark
    /// them read on arrival, so an ordinary click around the app costs no request at all.
    /// </summary>
    public bool HasUnreadFor(string url)
        => _unreadEntries.Any(entry => string.Equals(entry.Url, url, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The unread entries reaching <paramref name="path"/> settles: the ones pointing at exactly this
    /// page, and the ones pointing at a page this one sits under. Opening a task list's editor is
    /// reaching that task list, even though the editor's path is longer (see the two editing levels) -
    /// and a notification about a list you are looking at the innards of is one you have read.
    ///
    /// Matched at a path-segment boundary, so "/tasks/{a}" is settled by "/tasks/{a}/edit" and by
    /// nothing else: "/tasks/{b}" is not a prefix of it, and neither is "/tasks/{a}bc".
    /// </summary>
    public IReadOnlyList<string> UnreadUrlsSettledBy(string path)
        => [.. _unreadEntries
            .Select(entry => entry.Url)
            .OfType<string>()
            .Where(url => Settles(url, path))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static bool Settles(string notificationUrl, string path)
        => string.Equals(notificationUrl, path, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(notificationUrl + "/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Drops the entries pointing at url, matching what the server was just told. Applied locally rather
    /// than by re-fetching, so the badge clears as the page opens instead of on the next poll.
    /// </summary>
    public void MarkReadFor(string url)
        => Set(_unreadEntries
            .Where(entry => !string.Equals(entry.Url, url, StringComparison.OrdinalIgnoreCase))
            .ToList());
}
