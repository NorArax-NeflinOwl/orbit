namespace Orbit.Core.Notifications;

/// <summary>
/// Reading the address a notification carries. The address is a web path - it was written for the
/// browser, where the path <i>is</i> the destination - and both clients have to take it apart the same
/// way, so the taking apart lives beside the thing that writes them (see InventoryExpiryPushContent and
/// its siblings) rather than twice over in two clients.
///
/// An address may say two things: which page, and - for something that lives inside a page rather than
/// having one of its own - which row of it. A shelf row is the case: the warning about something going
/// off names the shelf and the row on it.
/// </summary>
public static class NotificationUrl
{
    /// <summary>What names a row inside a page, in an address that names one.</summary>
    private const string RowName = "highlight=";

    /// <summary>
    /// The page an address is, without what the reader is asked to do on arrival. What every comparison
    /// between two addresses is made on: nothing ever navigates to the row-naming spelling except the
    /// notification itself, so comparing addresses whole would leave such an entry unread for good.
    /// </summary>
    public static string PathOf(string url)
        => url[..(url.IndexOfAny(['?', '#']) is >= 0 and var at ? at : url.Length)];

    /// <summary>
    /// The row an address names, if it names one. Null for every other address, and for a row that is
    /// not an id - an address is data from the server, and a malformed one means "no row" rather than a
    /// page that fails to draw.
    /// </summary>
    public static Guid? RowNamedIn(string? url)
    {
        var at = (url ?? string.Empty).IndexOf('?');
        if (at < 0)
        {
            return null;
        }

        foreach (var part in url![(at + 1)..].Split('&'))
        {
            if (part.StartsWith(RowName, StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(part[RowName.Length..], out var row))
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>The address of <paramref name="path"/> with a row named on it - see <see cref="RowNamedIn"/>.</summary>
    public static string Naming(string path, Guid row) => $"{path}?{RowName}{row}";
}
