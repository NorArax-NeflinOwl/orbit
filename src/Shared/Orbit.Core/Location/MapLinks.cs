using System.Globalization;
using System.Text.RegularExpressions;

namespace Orbit.Core.Location;

/// <summary>
/// Somewhere a map link points at: the point it names, or the words it searched for, and the link it was
/// read from.
/// </summary>
/// <param name="Latitude">Null together with <paramref name="Longitude"/> for a link that names words rather than a point.</param>
/// <param name="Search">
/// What the link asked to be found - an address, a name - for one that carries no coordinates. Null
/// where the point is the answer. A reader with both is given the point: it is exact, and the words are
/// only how somebody happened to reach it.
/// </param>
/// <param name="Label">What the link calls the place, where it says - a name out of the path, usually.</param>
/// <param name="Url">The link exactly as it was written, which is what "open the original" opens.</param>
public sealed record MapLink(double? Latitude, double? Longitude, string? Search, string? Label, string Url)
{
    /// <summary>Whether this link says where it is, rather than what to look for.</summary>
    public bool HasAPoint => Latitude is not null && Longitude is not null;
}

/// <summary>
/// Reads a link to somebody else's map and works out where it points, so Orbit can show it on its own.
///
/// Asked for on 2026-09-19: a link pasted into a chat, or written in a note or on a task entry, should
/// open the place on Orbit's map - with the address, a way to start an event or a list there, a way to
/// be taken there, and the original link still one press away - rather than leaving Orbit for a map
/// somebody else runs.
///
/// Shared rather than the browser's own, because the phone draws the same words and reading a link in
/// two places is two rules that drift. It is also why this is a reader rather than a link builder:
/// GoogleMapsLink writes them, this one reads them, and the two have nothing to say to each other.
///
/// <b>What it does not do.</b> A shortened link (maps.app.goo.gl, goo.gl/maps) carries nothing but an
/// identifier: only the service that made it knows where it points, so one is recognised
/// (<see cref="IsAShortenedLink"/>) and left to a caller that can ask - see the API's resolver - rather
/// than guessed at here.
/// </summary>
public static class MapLinks
{
    /// <summary>
    /// The hosts worth reading. A list of what is known rather than a guess at what looks like a map:
    /// rewriting somebody's link to point at Orbit instead is a thing to do only where Orbit is certain
    /// what the link meant.
    /// </summary>
    private static readonly string[] KnownHosts =
    [
        "google.com", "maps.google.com", "goo.gl", "maps.app.goo.gl",
        "maps.apple.com", "openstreetmap.org", "osm.org", "bing.com", "waze.com"
    ];

    /// <summary>The shorteners, which say nothing about where they point until they are followed.</summary>
    private static readonly string[] ShortenerHosts = ["goo.gl", "maps.app.goo.gl"];

    /// <summary>
    /// A pair of coordinates written into a path rather than a query - Google's "@52.23,21.01,17z" and
    /// OpenStreetMap's "#map=17/52.23/21.01". Latitude first in both.
    /// </summary>
    private static readonly Regex PointInAPath = new(
        @"@(?<latitude>-?\d+\.?\d*),(?<longitude>-?\d+\.?\d*)",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex PointInAnOsmFragment = new(
        @"map=\d+\.?\d*/(?<latitude>-?\d+\.?\d*)/(?<longitude>-?\d+\.?\d*)",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Where this link points, or null for one that is not a map link at all - which is nearly every
    /// link, and the answer this is asked for most.
    ///
    /// Only http and https are read, the same rule LinksInText follows and for the same reason: a
    /// scheme this does not know is left as words rather than turned into something Orbit follows.
    /// </summary>
    public static MapLink? Read(string? url)
    {
        if (!TryReadHost(url, out var uri, out var host))
        {
            return null;
        }

        // A shortener says nothing on its own. Answered as a map link with neither a point nor words,
        // so a caller can tell "not a map" from "a map I cannot place yet".
        if (ShortenerHosts.Contains(host))
        {
            return new MapLink(null, null, Search: null, Label: null, url!);
        }

        var query = QueryOf(uri);
        var point = PointIn(query, uri);
        var label = LabelIn(uri, query);
        return point is { } found
            ? new MapLink(found.Latitude, found.Longitude, Search: null, label, url!)
            : SearchIn(query) is { } search
                ? new MapLink(null, null, search, label ?? search, url!)
                : null;
    }

    /// <summary>Whether this link is one only its own service can place - see the class comment.</summary>
    public static bool IsAShortenedLink(string? url)
        => TryReadHost(url, out _, out var host) && ShortenerHosts.Contains(host);

    /// <summary>
    /// The host without "www.", where this is an http(s) address of a map service. False for everything
    /// else, which is what most links are.
    /// </summary>
    private static bool TryReadHost(string? url, out Uri uri, out string host)
    {
        uri = null!;
        host = string.Empty;
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var withoutWww = parsed.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? parsed.Host[4..]
            : parsed.Host;

        // google.com is only a map where the path says so: the rest of it is a search engine.
        if (string.Equals(withoutWww, "google.com", StringComparison.OrdinalIgnoreCase)
            && !parsed.AbsolutePath.StartsWith("/maps", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // The same for bing.com, whose maps live under /maps as well.
        if (string.Equals(withoutWww, "bing.com", StringComparison.OrdinalIgnoreCase)
            && !parsed.AbsolutePath.StartsWith("/maps", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!KnownHosts.Contains(withoutWww, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        host = withoutWww.ToLowerInvariant();
        return true;
    }

    /// <summary>
    /// The query as a dictionary, with the keys lowered: the same parameter is spelled "q" by one
    /// service and "query" by the next, and neither is consistent about case.
    /// </summary>
    private static IReadOnlyDictionary<string, string> QueryOf(Uri uri)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = pair.Split('=', 2);
            if (split.Length == 2 && !parameters.ContainsKey(split[0]))
            {
                parameters[split[0]] = Uri.UnescapeDataString(split[1].Replace('+', ' '));
            }
        }

        return parameters;
    }

    /// <summary>
    /// The point, wherever this service writes it. The query is read first and the path second, because
    /// a Google link carries both and the query is the one somebody asked for - "@" in the path is the
    /// map's own centre, which a link to a place beside a road is not.
    /// </summary>
    private static (double Latitude, double Longitude)? PointIn(
        IReadOnlyDictionary<string, string> query, Uri uri)
    {
        // Every parameter any of these services puts a pair of coordinates in, in the order a reader
        // means them: what was asked for, then where to go, then where the map happens to sit.
        foreach (var name in new[] { "q", "query", "destination", "daddr", "ll", "sll", "cp", "center" })
        {
            if (query.TryGetValue(name, out var value) && AsAPoint(value) is { } found)
            {
                return found;
            }
        }

        // OpenStreetMap's own marker, which it writes as two parameters rather than one.
        if (query.TryGetValue("mlat", out var latitude) && query.TryGetValue("mlon", out var longitude)
            && AsANumber(latitude) is { } markerLatitude && AsANumber(longitude) is { } markerLongitude)
        {
            return (markerLatitude, markerLongitude);
        }

        var inThePath = PointInAPath.Match(uri.AbsolutePath);
        if (inThePath.Success)
        {
            return (
                AsANumber(inThePath.Groups["latitude"].Value)!.Value,
                AsANumber(inThePath.Groups["longitude"].Value)!.Value);
        }

        var inTheFragment = PointInAnOsmFragment.Match(uri.Fragment);
        return inTheFragment.Success
            ? (AsANumber(inTheFragment.Groups["latitude"].Value)!.Value,
                AsANumber(inTheFragment.Groups["longitude"].Value)!.Value)
            : null;
    }

    /// <summary>What the link asked to be found, for one that carries no coordinates.</summary>
    private static string? SearchIn(IReadOnlyDictionary<string, string> query)
    {
        foreach (var name in new[] { "q", "query", "destination", "daddr", "address" })
        {
            if (query.TryGetValue(name, out var value) && value.Trim() is { Length: > 0 } search)
            {
                return search;
            }
        }

        return null;
    }

    /// <summary>
    /// What the link calls the place. Google writes it into the path - "/maps/place/Pałac+Kultury/@..." -
    /// and Apple sends it as a parameter; neither is always there, and a place with no name of its own
    /// is named by the address the map page looks up.
    /// </summary>
    private static string? LabelIn(Uri uri, IReadOnlyDictionary<string, string> query)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var place = Array.IndexOf(segments, "place");
        if (place >= 0 && place + 1 < segments.Length)
        {
            var named = Uri.UnescapeDataString(segments[place + 1].Replace('+', ' ')).Trim();
            // "/place/52.23,21.01" is the point again rather than a name for it.
            if (named.Length > 0 && AsAPoint(named) is null)
            {
                return named;
            }
        }

        return query.TryGetValue("q", out var asked) && AsAPoint(asked) is null && asked.Trim().Length > 0
            ? asked.Trim()
            : null;
    }

    /// <summary>
    /// A pair written "52.2297,21.0122", which is how every one of these services writes one. A latitude
    /// past the poles is not a place: "q=100,200" is somebody's search text that happens to hold a comma,
    /// and reading it as a point would drop a pin in the sea.
    /// </summary>
    private static (double Latitude, double Longitude)? AsAPoint(string value)
    {
        // Bing separates the pair with "~" rather than a comma; everything else uses a comma.
        var parts = value.Split(value.Contains('~') ? '~' : ',');
        return parts.Length == 2
            && AsANumber(parts[0]) is { } latitude and >= -90 and <= 90
            && AsANumber(parts[1]) is { } longitude
            ? (latitude, longitude)
            : null;
    }

    /// <summary>
    /// Invariant, and that is load-bearing: a link is written with a full stop wherever it was made, and
    /// reading one under a Polish culture would make "52.2297" into 522297.
    /// </summary>
    private static double? AsANumber(string value)
        => double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && number is >= -180 and <= 180
            ? number
            : null;
}
