namespace Orbit.Web.Services;

/// <summary>
/// Where a screen should go when it is finished - the page that opened it, rather than wherever that
/// kind of thing is normally listed.
///
/// An editor used to end on the section it belongs to, whatever route reached it: saving a task list
/// landed on /tasks even for somebody who had opened it from the calendar, so finishing one small edit
/// meant navigating all the way back. The page that sends a reader in now says where it is, and the one
/// they finish on reads it.
///
/// It travels as a query parameter rather than being remembered somewhere, so it survives a reload and
/// a link somebody kept - and so a page arrived at without one still has an answer, which is the
/// section it belongs to.
///
/// <b>Only a path on this site is ever followed.</b> The value comes off the address bar, so it is
/// whatever anybody put there: without that rule a link to Orbit could carry somebody to another site
/// with Orbit's own "Save" as the thing that took them, which is the ordinary shape of an open
/// redirect. See <see cref="Safe"/> for what is allowed.
/// </summary>
public static class ReturnTo
{
    /// <summary>The name it travels under, in one place so the reader and the writer cannot disagree.</summary>
    public const string QueryName = "returnTo";

    /// <summary>
    /// The address if it is one this site may follow, or null. Allowed: a path beginning with a single
    /// "/". Refused: anything else - an absolute URL, a protocol-relative "//host" (which is another
    /// site however harmless it looks), and anything holding a backslash, since a browser may read one
    /// as the slash that turns "/\host" into "//host".
    /// </summary>
    public static string? Safe(string? returnTo)
        => returnTo is { Length: > 0 }
            && returnTo[0] == '/'
            && !returnTo.StartsWith("//", StringComparison.Ordinal)
            && !returnTo.Contains('\\', StringComparison.Ordinal)
            && !returnTo.Any(char.IsControl)
                ? returnTo
                : null;

    /// <summary>
    /// <paramref name="path"/> with "come back here afterwards" attached - what a page building a link
    /// into an editor uses. Nothing is attached when there is nowhere worth naming, so an ordinary link
    /// stays an ordinary link.
    ///
    /// The separator is chosen rather than assumed to be "?": most callers build the path themselves
    /// and know it has no query, but a notification's address comes off a row the server wrote, and a
    /// shared place is one - "/map?place={id}", since a place is met on the map rather than on a page of
    /// its own (see SharedItemNotifier.AddressOf). A second "?" is not a query at all: the browser reads
    /// everything after the first one as the query string, so the place id would arrive as
    /// "{id}?returnTo=/notifications" and the map would open on no pin.
    /// </summary>
    public static string Link(string path, string? comeBackTo)
        => Safe(comeBackTo) is { } destination
            ? $"{path}{(path.Contains('?', StringComparison.Ordinal) ? '&' : '?')}{QueryName}={Uri.EscapeDataString(destination)}"
            : path;

    /// <summary>
    /// Where to go now: what was asked for if it may be followed, and <paramref name="fallback"/> - the
    /// section this thing belongs to - otherwise. Every screen that ends somewhere goes through here, so
    /// a page reached by typing its address still finishes somewhere sensible.
    /// </summary>
    public static string Or(string? returnTo, string fallback) => Safe(returnTo) ?? fallback;

    /// <summary>
    /// Where to come back to once the reader moves on from the page at <paramref name="pagePath"/> to
    /// another of its kind: <paramref name="comeBackTo"/> as it is, unless it *is* that page - then that
    /// page's own way back, which it carries on its address. Moving from one note to the next in the note
    /// editor's column is the case: a note opened from its own page names that page, and handing the name
    /// on made finishing the second note end on the first one's page. Null when there is nowhere worth
    /// naming, which leaves the next screen its own section to fall back on.
    /// </summary>
    public static string? PastThePageOf(string? comeBackTo, string pagePath)
    {
        if (Safe(comeBackTo) is not { } destination)
        {
            return null;
        }

        var queryStart = destination.IndexOf('?', StringComparison.Ordinal);
        var path = queryStart < 0 ? destination : destination[..queryStart];
        if (!string.Equals(path, pagePath, StringComparison.OrdinalIgnoreCase))
        {
            return destination;
        }

        if (queryStart < 0)
        {
            return null;
        }

        foreach (var pair in destination[(queryStart + 1)..].Split('&'))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0 && pair[..separator] == QueryName)
            {
                return Safe(Uri.UnescapeDataString(pair[(separator + 1)..]));
            }
        }

        return null;
    }
}
