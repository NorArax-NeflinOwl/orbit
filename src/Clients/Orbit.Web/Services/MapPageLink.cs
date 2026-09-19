using System.Globalization;
using Orbit.Core.Location;

namespace Orbit.Web.Services;

/// <summary>
/// Where a link to somebody else's map opens inside Orbit - the map page, with the place already
/// pinned. Asked for on 2026-09-19: a link pasted into a chat, or written in a note or on a task entry,
/// should land on Orbit's own map with the address, a way to start an event or a list there, a way to be
/// taken there, and the original link still one press away.
///
/// Reading the link is MapLinks' job and shared with the phone; this is only the browser's address for
/// the answer, which is why it lives here.
/// </summary>
public static class MapPageLink
{
    /// <summary>The map page's own address - the three parameters below are read there.</summary>
    public const string Page = "/map";

    /// <summary>Where it is, written "latitude,longitude" the way every map service writes a pair.</summary>
    public const string PointParameter = "at";

    /// <summary>What to look for, where the link named words rather than a point.</summary>
    public const string SearchParameter = "find";

    /// <summary>The link it was read from, so the pin can still offer to open it where it came from.</summary>
    public const string SourceParameter = "from";

    /// <summary>
    /// Where inside Orbit this link opens, or null for one that is not a map link at all - which is left
    /// exactly as it was written, since rewriting a link to point somewhere else is a thing to do only
    /// where Orbit is certain what the link meant.
    ///
    /// A shortened link goes to the map carrying nothing but itself: only the service that made it knows
    /// where it points, and the map page asks the server to follow it (see ResolveMapLinkQuery) rather
    /// than guessing here. Where that comes back with nothing, the page says so and offers the link.
    /// </summary>
    public static string? For(string? url)
        => MapLinks.Read(url) is { } link ? For(link) : null;

    /// <inheritdoc cref="For(string?)"/>
    public static string? For(MapLink link)
    {
        if (link.HasAPoint)
        {
            var point = string.Create(
                CultureInfo.InvariantCulture, $"{link.Latitude!.Value},{link.Longitude!.Value}");
            return $"{Page}?{PointParameter}={Uri.EscapeDataString(point)}{Named(link)}{From(link)}";
        }

        return link.Search is { Length: > 0 } search
            ? $"{Page}?{SearchParameter}={Uri.EscapeDataString(search)}{From(link)}"
            : $"{Page}?{SourceParameter}={Uri.EscapeDataString(link.Url)}";
    }

    /// <summary>
    /// What the link called the place, where it said - so the pin is named before anything is looked up.
    /// Left out where the name is the search text itself, which the page would only be repeating.
    /// </summary>
    private static string Named(MapLink link)
        => link.Label is { Length: > 0 } label ? $"&label={Uri.EscapeDataString(label)}" : string.Empty;

    private static string From(MapLink link) => $"&{SourceParameter}={Uri.EscapeDataString(link.Url)}";
}
