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
    /// </summary>
    public static string Link(string path, string? comeBackTo)
        => Safe(comeBackTo) is { } destination
            ? $"{path}?{QueryName}={Uri.EscapeDataString(destination)}"
            : path;

    /// <summary>
    /// Where to go now: what was asked for if it may be followed, and <paramref name="fallback"/> - the
    /// section this thing belongs to - otherwise. Every screen that ends somewhere goes through here, so
    /// a page reached by typing its address still finishes somewhere sensible.
    /// </summary>
    public static string Or(string? returnTo, string fallback) => Safe(returnTo) ?? fallback;
}
