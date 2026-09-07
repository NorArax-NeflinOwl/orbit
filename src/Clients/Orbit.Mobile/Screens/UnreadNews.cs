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
        => string.Equals(address, unread, StringComparison.OrdinalIgnoreCase)
            || unread.StartsWith(address + "/", StringComparison.OrdinalIgnoreCase);
}
