using System.Net;
using Orbit.Mobile.Location;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Location;

/// <summary>
/// Looking an address up, so a place can be named rather than only pointed at. Nominatim's own shape is
/// what is being read here: coordinates as strings, and a written form that is the only thing telling
/// two matches apart.
/// </summary>
public sealed class PlaceSearchTests
{
    [Fact]
    public async Task Every_match_comes_back_in_the_order_it_was_given()
    {
        using var server = StubHttpMessageHandler.RespondingWith(new[]
        {
            new { lat = "52.2297", lon = "21.0122", display_name = "Długa 4, Warszawa" },
            new { lat = "51.1079", lon = "17.0385", display_name = "Długa 4, Wrocław" }
        });

        var matches = await new PlaceSearch(server.ToHttpClient()).SearchAsync("Długa 4");

        Assert.Equal(["Długa 4, Warszawa", "Długa 4, Wrocław"], matches.Select(match => match.Name));
        Assert.Equal(52.2297, matches[0].Latitude, precision: 4);
    }

    /// <summary>
    /// Nominatim returns the coordinates as strings, and always with a dot - a reader whose own calendar
    /// writes a comma must not turn 52.2297 into 522297, which is somewhere else entirely.
    /// </summary>
    [Fact]
    public async Task Coordinates_are_read_the_same_wherever_the_reader_is()
    {
        using var server = StubHttpMessageHandler.RespondingWith(new[]
        {
            new { lat = "52.2297", lon = "21.0122", display_name = "Warszawa" }
        });

        var match = Assert.Single(await new PlaceSearch(server.ToHttpClient()).SearchAsync("Warszawa"));

        Assert.Equal(52.2297, match.Latitude, precision: 4);
        Assert.Equal(21.0122, match.Longitude, precision: 4);
    }

    /// <summary>A match nobody can place is no match at all, and must not take the others down with it.</summary>
    [Fact]
    public async Task A_match_whose_coordinates_will_not_parse_is_left_out()
    {
        using var server = StubHttpMessageHandler.RespondingWith(new[]
        {
            new { lat = "nowhere", lon = "21.0122", display_name = "Nowhere" },
            new { lat = "52.2297", lon = "21.0122", display_name = "Warszawa" }
        });

        var match = Assert.Single(await new PlaceSearch(server.ToHttpClient()).SearchAsync("Warszawa"));

        Assert.Equal("Warszawa", match.Name);
    }

    /// <summary>
    /// Nothing found and a lookup that failed read the same way - "nothing found for that" is the truth
    /// either way, and neither is worth an error over an address somebody can still type.
    /// </summary>
    [Fact]
    public async Task A_lookup_that_cannot_be_made_finds_nothing()
    {
        using var server = StubHttpMessageHandler.Unreachable();

        Assert.Empty(await new PlaceSearch(server.ToHttpClient()).SearchAsync("Warszawa"));
    }

    [Fact]
    public async Task A_refused_lookup_finds_nothing()
    {
        using var server = StubHttpMessageHandler.RespondingWith(HttpStatusCode.TooManyRequests);

        Assert.Empty(await new PlaceSearch(server.ToHttpClient()).SearchAsync("Warszawa"));
    }

    /// <summary>An empty box is not a search, and asking Nominatim for nothing is asking for a refusal.</summary>
    [Fact]
    public async Task An_empty_box_asks_nobody_anything()
    {
        using var server = StubHttpMessageHandler.Unreachable();

        Assert.Empty(await new PlaceSearch(server.ToHttpClient()).SearchAsync("   "));
        Assert.Empty(server.ReceivedRequests);
    }
}
