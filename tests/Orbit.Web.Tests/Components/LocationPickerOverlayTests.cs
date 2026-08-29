using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Web.Components;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// Saying where something happens by pointing at it. Nothing is written back until the pin is
/// confirmed: a stray click on a map must not silently rewrite an address somebody typed.
/// </summary>
public sealed class LocationPickerOverlayTests : OrbitTestContext
{
    public LocationPickerOverlayTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        // Leaflet is not loaded here, so the module answers both calls with nothing. What these tests
        // are about is what the overlay asks and what it reports back.
        var mapPicker = JSInterop.SetupModule("./js/mapPicker.js");
        mapPicker.SetupVoid("initializeMapPicker", _ => true).SetVoidResult();
        mapPicker.SetupVoid("disposeMapPicker", _ => true).SetVoidResult();
        mapPicker.SetupVoid("moveMarker", _ => true).SetVoidResult();
    }

    [Fact]
    public void Nothing_is_asked_until_a_pin_is_dropped()
    {
        RegisterGeocoding("Długa 4, Warszawa");

        var cut = Render();

        Assert.Contains("Click the map to drop a pin.", cut.Markup);
        Assert.Empty(cut.FindAll(".map-overlay-confirm"));
    }

    [Fact]
    public async Task Dropping_a_pin_asks_whether_to_use_that_place()
    {
        RegisterGeocoding("Długa 4, Warszawa");
        var cut = Render();

        await DropAPinAsync(cut);

        Assert.Contains("Długa 4, Warszawa", cut.Find(".map-overlay-address").TextContent);
        Assert.Contains("Use this place?", cut.Markup);
    }

    [Fact]
    public async Task A_confirmed_pin_is_the_only_thing_that_reports_an_address()
    {
        RegisterGeocoding("Długa 4, Warszawa");
        string? reported = null;
        var cut = Render(onPicked: address => reported = address);
        await DropAPinAsync(cut);

        cut.FindAll(".map-overlay-confirm button").First(button => button.TextContent.Contains("Yes")).Click();

        Assert.Equal("Długa 4, Warszawa", reported);
    }

    [Fact]
    public async Task Confirming_takes_the_map_down_with_it()
    {
        RegisterGeocoding("Długa 4, Warszawa");
        var cut = Render();
        await DropAPinAsync(cut);

        cut.FindAll(".map-overlay-confirm button").First(button => button.TextContent.Contains("Yes")).Click();

        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == "disposeMapPicker");
    }

    [Fact]
    public async Task Backing_out_reports_nothing_at_all()
    {
        // The address box behind the overlay keeps whatever it already held.
        RegisterGeocoding("Długa 4, Warszawa");
        string? reported = null;
        var cancelled = false;
        var cut = Render(onPicked: address => reported = address, onCancelled: () => cancelled = true);
        await DropAPinAsync(cut);

        cut.FindAll(".map-overlay-confirm button").First(button => button.TextContent.Contains("Cancel")).Click();

        Assert.Null(reported);
        Assert.True(cancelled);
    }

    [Fact]
    public async Task A_place_with_no_address_says_so_rather_than_offering_an_empty_answer()
    {
        // Open water, a field, anywhere Nominatim has no name for.
        RegisterGeocoding(displayName: null);
        var cut = Render();

        await DropAPinAsync(cut);

        Assert.Contains("only the pin says where it is", cut.Markup);
    }


    [Fact]
    public void An_address_can_be_named_as_well_as_pointed_at()
    {
        // Which is the way that works when somebody knows the address but not where it is on a map.
        RegisterGeocoding("Długa 4, Warszawa");

        var cut = Render();

        Assert.Single(cut.FindAll(".map-overlay-search-box"));
    }

    [Fact]
    public void A_search_offers_every_place_the_name_could_mean()
    {
        // Street names repeat: "Długa 4" is a real address in a dozen towns, and quietly taking the
        // first would drop a pin in whichever of them Nominatim happened to rank first.
        RegisterGeocoding(null, searchBody: TwoMatches);
        var cut = Render();

        SearchFor(cut, "Długa 4");

        var offered = cut.FindAll(".map-overlay-match").Select(match => match.TextContent).ToList();
        Assert.Equal(["Długa 4, Warszawa", "Długa 4, Gdańsk"], offered);
    }

    [Fact]
    public void Picking_one_of_them_asks_whether_to_use_it()
    {
        RegisterGeocoding(null, searchBody: TwoMatches);
        var cut = Render();
        SearchFor(cut, "Długa 4");

        cut.FindAll(".map-overlay-match").First(match => match.TextContent.Contains("Gdańsk")).Click();

        Assert.Contains("Długa 4, Gdańsk", cut.Find(".map-overlay-address").TextContent);
        Assert.Contains("Use this place?", cut.Markup);
    }

    [Fact]
    public void Picking_one_of_them_moves_the_pin_there()
    {
        RegisterGeocoding(null, searchBody: TwoMatches);
        var cut = Render();
        SearchFor(cut, "Długa 4");

        cut.FindAll(".map-overlay-match").First(match => match.TextContent.Contains("Gdańsk")).Click();

        var moved = Assert.Single(JSInterop.Invocations, invocation => invocation.Identifier == "moveMarker");
        Assert.Equal(54.35, moved.Arguments[1]);
        Assert.Equal(18.65, moved.Arguments[2]);
    }

    [Fact]
    public void The_address_that_gets_saved_is_the_one_that_was_picked()
    {
        // Taken from the search rather than looked up a second time: it is the name they picked by, and
        // a second lookup could answer differently.
        RegisterGeocoding("Somewhere else entirely", searchBody: TwoMatches);
        string? reported = null;
        var cut = Render(onPicked: address => reported = address);
        SearchFor(cut, "Długa 4");
        cut.FindAll(".map-overlay-match").First(match => match.TextContent.Contains("Warszawa")).Click();

        cut.FindAll(".map-overlay-confirm button").First(button => button.TextContent.Contains("Yes")).Click();

        Assert.Equal("Długa 4, Warszawa", reported);
    }

    [Fact]
    public void One_answer_is_not_a_choice_and_is_taken_as_one()
    {
        // Asking somebody to confirm the only match twice - once as a row, once as the question - is
        // asking the same thing twice.
        RegisterGeocoding(null, searchBody: OneMatch);
        var cut = Render();

        SearchFor(cut, "Długa 4 Warszawa");

        Assert.Empty(cut.FindAll(".map-overlay-match"));
        Assert.Contains("Długa 4, Warszawa", cut.Find(".map-overlay-address").TextContent);
    }

    [Fact]
    public void A_name_nothing_matches_says_so_rather_than_doing_nothing()
    {
        RegisterGeocoding(null, searchBody: "[]");
        var cut = Render();

        SearchFor(cut, "Nowhere at all");

        Assert.Contains("Nothing found for that", cut.Markup);
        Assert.Empty(cut.FindAll(".map-overlay-confirm"));
    }

    [Fact]
    public void Nothing_is_said_about_a_search_nobody_has_run()
    {
        RegisterGeocoding("Długa 4, Warszawa");

        var cut = Render();

        Assert.DoesNotContain("Nothing found for that", cut.Markup);
    }

    /// <summary>Two towns with the same street, which is the case the list of matches exists for.</summary>
    private const string TwoMatches = """
        [{"lat":"52.2497","lon":"21.0122","display_name":"Długa 4, Warszawa"},
         {"lat":"54.35","lon":"18.65","display_name":"Długa 4, Gdańsk"}]
        """;

    private const string OneMatch = """[{"lat":"52.2497","lon":"21.0122","display_name":"Długa 4, Warszawa"}]""";

    private static void SearchFor(IRenderedComponent<LocationPickerOverlay> cut, string address)
    {
        cut.Find(".map-overlay-search-box").Input(address);
        cut.FindAll(".map-overlay-search button").First().Click();
    }
    private IRenderedComponent<LocationPickerOverlay> Render(
        Action<string>? onPicked = null, Action? onCancelled = null)
        => RenderComponent<LocationPickerOverlay>(parameters => parameters
            .Add(overlay => overlay.Address, "Warszawa")
            .Add(overlay => overlay.OnPicked, address => onPicked?.Invoke(address))
            .Add(overlay => overlay.OnCancelled, () => onCancelled?.Invoke()));

    /// <summary>What mapPicker.js does when somebody clicks the map.</summary>
    private static async Task DropAPinAsync(IRenderedComponent<LocationPickerOverlay> cut)
        => await cut.InvokeAsync(() => cut.Instance.OnMapLocationPicked(52.2497, 21.0122));

    /// <param name="displayName">Null stands for a point Nominatim has no address for.</param>
    /// <param name="searchBody">What an address search answers with - nothing found, by default.</param>
    private void RegisterGeocoding(string? displayName, string searchBody = "[]")
    {
        var body = displayName is null ? "{}" : $$"""{"display_name":"{{displayName}}"}""";
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                // Both directions come from the same host: "search" is an address being looked up -
                // the one that decides where the map opens, and the one the search box runs.
                Content = new StringContent(
                    request.RequestUri!.AbsolutePath.Contains("search", StringComparison.Ordinal) ? searchBody : body,
                    Encoding.UTF8,
                    "application/json")
            }))
        {
            BaseAddress = new Uri("https://geocode.test/")
        };
        Services.AddSingleton(new GeocodingApiClient(httpClient));
    }
}
