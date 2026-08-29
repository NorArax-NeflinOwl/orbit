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
    private void RegisterGeocoding(string? displayName)
    {
        var body = displayName is null ? "{}" : $$"""{"display_name":"{{displayName}}"}""";
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                // The forward search that decides where the map opens answers with nothing found; only
                // the reverse lookup matters to these tests.
                Content = new StringContent(
                    request.RequestUri!.AbsolutePath.Contains("search", StringComparison.Ordinal) ? "[]" : body,
                    Encoding.UTF8,
                    "application/json")
            }))
        {
            BaseAddress = new Uri("https://geocode.test/")
        };
        Services.AddSingleton(new GeocodingApiClient(httpClient));
    }
}
