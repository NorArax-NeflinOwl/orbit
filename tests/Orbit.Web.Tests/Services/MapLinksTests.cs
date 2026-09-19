using System.Globalization;
using Orbit.Core.Location;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Reading a link to somebody else's map, so Orbit can show the place on its own - see MapLinks, and
/// the request of 2026-09-19 behind it.
///
/// Every one of these is a real shape one of those services hands out, read once from a share sheet and
/// then never looked at again: a wrong parameter here means a pin in the wrong place, or an ordinary
/// link quietly rewritten to point at Orbit and no longer at what it said.
/// </summary>
public sealed class MapLinksTests
{
    [Theory]
    // The map's own centre, which a "share this view" link is.
    [InlineData("https://www.google.com/maps/@52.2297,21.0122,15z")]
    // A place, with its name in the path and the centre after it.
    [InlineData("https://www.google.com/maps/place/Pa%C5%82ac+Kultury/@52.2297,21.0122,17z/data=!3m1")]
    // The documented "Maps URLs" search, which is what a "share" button gives.
    [InlineData("https://www.google.com/maps/search/?api=1&query=52.2297,21.0122")]
    [InlineData("https://maps.google.com/?q=52.2297,21.0122")]
    // Directions to it, which is the other half of the same documented API.
    [InlineData("https://www.google.com/maps/dir/?api=1&destination=52.2297,21.0122")]
    [InlineData("https://maps.apple.com/?ll=52.2297,21.0122&q=Dom")]
    [InlineData("https://www.openstreetmap.org/?mlat=52.2297&mlon=21.0122#map=17/52.2297/21.0122")]
    [InlineData("https://www.openstreetmap.org/#map=17/52.2297/21.0122")]
    [InlineData("https://www.bing.com/maps?cp=52.2297~21.0122")]
    public void A_link_that_names_a_point_gives_that_point(string url)
    {
        var link = MapLinks.Read(url);

        Assert.NotNull(link);
        Assert.True(link.HasAPoint);
        Assert.Equal(52.2297, link.Latitude!.Value, 4);
        Assert.Equal(21.0122, link.Longitude!.Value, 4);
        Assert.Equal(url, link.Url);
    }

    /// <summary>
    /// The one that is load-bearing: a link is written with a full stop wherever it was made, and a
    /// Polish thread reading "52.2297" by its own rules would land on 522297.
    /// </summary>
    [Fact]
    public void A_point_is_read_the_same_way_whatever_language_the_reader_is_in()
    {
        var wasCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
        try
        {
            var link = MapLinks.Read("https://www.google.com/maps/search/?api=1&query=52.2297,21.0122");

            Assert.Equal(52.2297, link!.Latitude!.Value, 4);
        }
        finally
        {
            CultureInfo.CurrentCulture = wasCulture;
        }
    }

    [Fact]
    public void A_place_link_carries_the_name_it_was_shared_under()
    {
        var link = MapLinks.Read("https://www.google.com/maps/place/Pa%C5%82ac+Kultury/@52.2297,21.0122,17z");

        Assert.Equal("Pałac Kultury", link!.Label);
    }

    /// <summary>
    /// A link that says what to look for rather than where it is. The map page has a search of its own,
    /// so the words are worth carrying - a pin cannot be dropped on them here.
    /// </summary>
    [Theory]
    [InlineData("https://www.google.com/maps/search/?api=1&query=Pi%C4%99kna+1,+Warszawa")]
    [InlineData("https://maps.apple.com/?address=Pi%C4%99kna+1,+Warszawa")]
    public void A_link_that_names_words_gives_the_words(string url)
    {
        var link = MapLinks.Read(url);

        Assert.NotNull(link);
        Assert.False(link.HasAPoint);
        Assert.Equal("Piękna 1, Warszawa", link.Search);
    }

    /// <summary>
    /// A shortened link carries an identifier and nothing else: only the service that made it knows
    /// where it points. Read as a map link with neither a point nor words, so a caller can tell it from
    /// a link that is not a map at all.
    /// </summary>
    [Theory]
    [InlineData("https://maps.app.goo.gl/AbCdEf123")]
    [InlineData("https://goo.gl/maps/AbCdEf123")]
    public void A_shortened_link_is_known_to_be_a_map_and_nothing_more(string url)
    {
        var link = MapLinks.Read(url);

        Assert.NotNull(link);
        Assert.False(link.HasAPoint);
        Assert.Null(link.Search);
        Assert.True(MapLinks.IsAShortenedLink(url));
    }

    /// <summary>
    /// Everything else is left exactly as it was written. Rewriting somebody's link to point at Orbit is
    /// a thing to do only where Orbit is certain what the link meant - see MapLinks' list of hosts.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/maps/@52.2297,21.0122,15z")]
    [InlineData("https://www.google.com/search?q=52.2297,21.0122")]
    [InlineData("https://www.google.com/maps")]
    [InlineData("https://www.bing.com/search?q=warszawa")]
    [InlineData("not a link at all")]
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_is_not_a_map_link(string? url)
        => Assert.Null(MapLinks.Read(url));

    /// <summary>
    /// A comma in somebody's search text is not a pair of coordinates. "100,200" is past both poles, and
    /// reading it as a point would drop a pin in the sea rather than search for what was typed.
    /// </summary>
    [Fact]
    public void Numbers_that_are_not_a_place_are_read_as_words()
    {
        var link = MapLinks.Read("https://www.google.com/maps/search/?api=1&query=100,200");

        Assert.False(link!.HasAPoint);
        Assert.Equal("100,200", link.Search);
    }

    /// <summary>
    /// What the reader asked for beats where the map happened to be sitting: a Google place link carries
    /// both, and the query is the half somebody pressed.
    /// </summary>
    [Fact]
    public void What_was_asked_for_beats_where_the_map_was_centred()
    {
        var link = MapLinks.Read(
            "https://www.google.com/maps/search/?api=1&query=50.0614,19.9366&center=52.2297,21.0122");

        Assert.Equal(50.0614, link!.Latitude!.Value, 4);
    }
}
