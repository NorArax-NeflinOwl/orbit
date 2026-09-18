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
    /// Whether anything unread is about this thing - the entry pointing at exactly its page, or at a
    /// page underneath it. What lets a list of things say *which* of them the bell is talking about:
    /// the dashboard reads it per row (see Dashboard.razor), so a notification about one task list
    /// marks that list rather than only the card it sits on.
    ///
    /// The same rule <see cref="UnreadUrlsSettledBy"/> reads the other way round, and matched at a
    /// path-segment boundary for the same reason - see <see cref="Settles"/>.
    /// </summary>
    public bool HasNewsAbout(string url)
        => _unreadEntries.Any(entry => entry.Url is { } entryUrl && Settles(url, entryUrl));

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

    /// <summary>
    /// Whether anything unread is about one thing <b>inside</b> a page, rather than about a page of its
    /// own - a row on a shelf, say. Such an entry points at the page and names the row it means with the
    /// "highlight" the whole app already uses to land on a row (see TaskListChecklist's links to a shelf
    /// item, and InventorySummary.Highlight).
    ///
    /// Kept apart from <see cref="HasNewsAbout(string)"/> because it asks a narrower question: the page
    /// still has news, and this says which row of it.
    /// </summary>
    public bool HasNewsAbout(string url, Guid thingOnThePage)
        => ThingsNamedFor(url).Contains(thingOnThePage);

    /// <summary>
    /// Every row on this page the unread entries name - see <see cref="HasNewsAbout(string, Guid)"/>.
    ///
    /// Asked all at once by the page those rows are on, and for a reason: arriving there is what marks
    /// those entries read (see NewsSettler, which MainLayout runs on every navigation), so a page that
    /// asked row by row as it drew would be asking a set that is about to empty. It takes this the
    /// moment it opens and keeps it for the visit.
    /// </summary>
    public IReadOnlyList<Guid> ThingsNamedFor(string url)
        => [.. _unreadEntries
            .Select(entry => entry.Url)
            .OfType<string>()
            .Where(entryUrl => string.Equals(PathOf(entryUrl), PathOf(url), StringComparison.OrdinalIgnoreCase))
            .Select(ThingNamedIn)
            .OfType<Guid>()
            .Distinct()];

    private static bool Settles(string notificationUrl, string path)
    {
        // The page each address is, without what it was asked to do on arrival: an entry about a row
        // points at its page and names the row after a "?" (see the overload above), and a reader who
        // opens that page has read it. Comparing the addresses whole would leave such an entry lit for
        // good - nothing ever navigates to the "?highlight=" spelling except the notification itself.
        var notificationPath = PathOf(notificationUrl);
        var reached = PathOf(path);
        return string.Equals(notificationPath, reached, StringComparison.OrdinalIgnoreCase)
            || reached.StartsWith(notificationPath + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The address without its query or fragment - see <see cref="Settles"/>.</summary>
    private static string PathOf(string url)
        => url[..(url.IndexOfAny(['?', '#']) is >= 0 and var at ? at : url.Length)];

    /// <summary>
    /// The row an entry's address names, if it names one: "?highlight={id}". Null for every other entry,
    /// and for a "highlight" that is not an id - an address is data from the server, and a malformed one
    /// means "no row" rather than a page that fails to draw.
    /// </summary>
    private static Guid? ThingNamedIn(string url)
    {
        const string names = "highlight=";
        var at = url.IndexOf('?');
        if (at < 0)
        {
            return null;
        }

        foreach (var part in url[(at + 1)..].Split('&'))
        {
            if (part.StartsWith(names, StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(part[names.Length..], out var thing))
            {
                return thing;
            }
        }

        return null;
    }

    /// <summary>
    /// Drops the entries pointing at url, matching what the server was just told. Applied locally rather
    /// than by re-fetching, so the badge clears as the page opens instead of on the next poll.
    /// </summary>
    public void MarkReadFor(string url)
        => Set(_unreadEntries
            .Where(entry => !string.Equals(entry.Url, url, StringComparison.OrdinalIgnoreCase))
            .ToList());
}
