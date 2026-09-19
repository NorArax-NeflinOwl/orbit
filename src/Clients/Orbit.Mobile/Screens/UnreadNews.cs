using Orbit.Contracts.Notifications;

namespace Orbit.Mobile.Screens;

/// <summary>
/// Which of the unread notifications is about a given thing - Orbit.Web's NotificationFeedState
/// answering the same question, so a card marked on one client is marked on the other.
///
/// A screen reads the unread entries once and asks this about each row it draws. The alternative -
/// every row asking the database about itself - is one round trip per row on screens made to be
/// scrolled.
/// </summary>
public static class UnreadNews
{
    /// <summary>The addresses worth keeping out of a list of unread entries: the ones that point somewhere.</summary>
    public static IReadOnlyList<string> AddressesIn(IEnumerable<NotificationEntryDto> unread)
        => [.. unread.Select(entry => entry.Url).OfType<string>().Where(url => url.Length > 0)];

    /// <summary>
    /// Whether anything unread is about <paramref name="address"/>. Matched at a path-segment boundary,
    /// which is what makes "/tasks/{a}" news for a notification pointing at "/tasks/{a}/edit" and not
    /// for one pointing at "/tasks/{b}" - the plain prefix test this replaces would also have taken
    /// "/tasks/{a}bc", which is a different list with a name that happens to start the same way.
    /// </summary>
    public static bool About(IReadOnlyList<string> addresses, string address)
        => addresses.Any(unread => Settles(address, unread));

    private static bool Settles(string address, string unread)
    {
        // Without what the reader is asked to do on arrival. An address may name a row inside the page
        // it points at - "?highlight={itemId}", which is how a warning about something going off names
        // the shelf row - and compared whole, such an entry marked nothing at all: not the shelf, whose
        // address is the part before the "?", and not the section above it. Orbit.Core's NotificationUrl
        // is where these addresses are written and now where both clients take them apart.
        var pointsAt = Orbit.Core.Notifications.NotificationUrl.PathOf(unread);
        return string.Equals(address, pointsAt, StringComparison.OrdinalIgnoreCase)
            || pointsAt.StartsWith(address + "/", StringComparison.OrdinalIgnoreCase);
    }
}
