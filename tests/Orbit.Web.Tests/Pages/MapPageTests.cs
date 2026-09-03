using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// The map screen's left panel and the search across the top of its map. The map itself is Leaflet's,
/// drawn through JS interop, so what is asserted here is everything around it: what the search does with
/// what it finds, and what pressing Create makes of the pin.
/// </summary>
public sealed class MapPageTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();

    public MapPageTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        // The page draws a Leaflet map through an imported module on every render. There is no map in a
        // test renderer, and none of this is about one - loose interop answers the import and the calls
        // that follow it with nothing.
        JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterEverythingThePageAsksFor();
    }

    [Fact]
    public void The_map_is_offered_only_to_an_account_that_has_unlocked_locations()
    {
        var cut = RenderComponent<MapPage>();

        // Nothing granted - see RegisterPermissions. The page says so rather than asking and being
        // turned away.
        Assert.Empty(cut.FindAll(".map-page"));
    }

    /// <summary>
    /// With nothing recorded, Start is the button and Update/Stop are what the menu offers about a
    /// recording that does not exist yet - so it is offered and greyed rather than absent, which is
    /// what makes the menu the same list every time.
    /// </summary>
    [Fact]
    public void Nothing_recorded_yet_offers_only_starting_it()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();

        Assert.False(ButtonSaying(cut, "Start").HasAttribute("disabled"));
        Assert.DoesNotContain(cut.FindAll(".map-panel-actions button"), button => button.TextContent.Contains("Stop"));

        cut.Find(".overflow-menu-trigger").Click();
        Assert.True(ButtonSaying(cut, "Update to where I am now").HasAttribute("disabled"));
    }

    /// <summary>
    /// Once a recording exists, Stop takes Start's own place rather than staying buried in the menu -
    /// the button used to just grey out, with no way back to it except through Stop recording there.
    /// </summary>
    [Fact]
    public void A_recording_that_exists_offers_Stop_in_starts_own_place()
    {
        GrantLocations();
        _ownLocationJson = OwnLocation();
        var cut = RenderComponent<MapPage>();

        Assert.DoesNotContain(cut.FindAll(".map-panel-actions button"), button => button.TextContent.Contains("Start"));
        var stop = ButtonSaying(cut, "Stop");
        Assert.False(stop.HasAttribute("disabled"));

        stop.Click();

        Assert.Contains(_deletedPaths, path => path.EndsWith("/location", StringComparison.Ordinal));
    }

    private static AngleSharp.Dom.IElement ButtonSaying(IRenderedFragment cut, string label)
        => cut.FindAll("button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal));

    [Fact]
    public void Searching_pins_what_it_found_and_asks_what_to_make_of_it()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();

        Search(cut, "Długa 4");

        Assert.Contains("Długa 4, Warszawa", cut.Find(".map-create-event").TextContent);
    }

    /// <summary>
    /// The pin goes on the best answer, because that is what somebody searching expects to see. The
    /// others are offered underneath rather than guessed between - street names repeat.
    /// </summary>
    [Fact]
    public void The_other_matches_stay_on_offer()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();

        Search(cut, "Długa 4");

        var offered = cut.FindAll(".map-canvas-matches button").Select(match => match.TextContent.Trim());
        Assert.Equal(["Długa 4, Kraków"], offered);
    }

    [Fact]
    public void A_search_that_finds_nothing_says_so_and_pins_nothing()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();

        Search(cut, "nowhere at all");

        Assert.Contains("Nothing found", cut.Find(".map-canvas-note").TextContent);
        Assert.Empty(cut.FindAll(".map-create-event"));
    }

    /// <summary>
    /// Confirming the pin and saying what it is for are separate questions - "is this the place" is
    /// answered by looking at the map, and "what happens here" is not.
    /// </summary>
    [Fact]
    public void Confirming_the_pin_asks_what_happens_there()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();
        Search(cut, "Długa 4");

        UseThePlace(cut);

        var asked = cut.Find(".map-overlay-panel").TextContent;
        Assert.Contains("What happens here?", asked);
        Assert.Contains("An event in the calendar", asked);
        Assert.Contains("A task list starting here", asked);
    }

    /// <summary>
    /// A place is not an appointment: the answer hands the pin to the editor that makes something of it
    /// rather than writing an event nobody has said when is - see ChosenPlace.
    /// </summary>
    [Theory]
    [InlineData("An event in the calendar", "/calendar/new")]
    [InlineData("A task list starting here", "/tasks/new")]
    public void The_answer_hands_the_pin_to_the_editor_that_makes_it(string answer, string url)
    {
        GrantLocations();
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var chosenPlace = Services.GetRequiredService<ChosenPlace>();
        var cut = RenderComponent<MapPage>();
        Search(cut, "Długa 4");
        UseThePlace(cut);

        cut.FindAll(".map-overlay-confirm button").First(button => button.TextContent.Contains(answer)).Click();

        Assert.EndsWith(url, navigationManager.Uri);
        var handedOver = chosenPlace.Take();
        Assert.NotNull(handedOver);
        Assert.Equal("Długa 4, Warszawa", handedOver.Address);
        Assert.Equal(52.25, handedOver.Latitude);
    }

    [Fact]
    public void Cancelling_takes_the_pin_off_again()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();
        Search(cut, "Długa 4");

        cut.FindAll(".map-create-event button").First(button => button.TextContent.Contains("Cancel")).Click();

        Assert.Empty(cut.FindAll(".map-create-event"));
    }

    /// <summary>
    /// The other way to pin a place: pressing the map itself. A map is dragged and zoomed by pressing
    /// it, so a press is easy to make by accident - it asks before anything moves, and only the answer
    /// puts a pin down.
    /// </summary>
    [Fact]
    public async Task Pressing_the_map_asks_before_it_pins_anything()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();
        Assert.Contains("Click the map to drop a pin.", cut.Find(".map-canvas-note").TextContent);

        await cut.InvokeAsync(() => cut.Instance.OnMapPressed(54.354, 18.656));

        var asked = cut.Find(".map-press-asks").TextContent;
        Assert.Contains("Wały Piastowskie 1, Gdańsk", asked);
        Assert.Contains("Put a pin here?", asked);
        // Nothing is pinned yet, so there is nothing to be asked what to make of.
        Assert.Empty(cut.FindAll(".map-create-event:not(.map-press-asks)"));

        YesTo(cut, ".map-press-asks");

        Assert.Contains("Wały Piastowskie 1, Gdańsk", cut.Find(".map-create-event:not(.map-press-asks)").TextContent);
    }

    /// <summary>
    /// "No" leaves the map exactly as it was - which is the whole point of asking. A pin somebody has
    /// already placed survives a stray press on the way to dragging the map.
    /// </summary>
    [Fact]
    public async Task A_press_that_is_refused_leaves_the_pin_where_it_was()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();
        Search(cut, "Długa 4");

        await cut.InvokeAsync(() => cut.Instance.OnMapPressed(54.354, 18.656));
        Assert.Contains("Move the pin here?", cut.Find(".map-press-asks").TextContent);

        cut.FindAll(".map-press-asks button").First(button => button.TextContent.Contains("No")).Click();

        Assert.Empty(cut.FindAll(".map-press-asks"));
        Assert.Contains("Długa 4, Warszawa", cut.Find(".map-create-event").TextContent);
    }

    private static void YesTo(IRenderedFragment cut, string selector)
        => cut.FindAll($"{selector} button").First(button => button.TextContent.Trim() == "Yes").Click();

    /// <summary>
    /// A field, or the sea. Somebody who pressed there meant that spot whether or not it has a street,
    /// so it is pinned and named by its numbers rather than refused.
    /// </summary>
    [Fact]
    public async Task A_place_with_no_address_is_pinned_by_its_coordinates()
    {
        GrantLocations();
        _reverseGeocodedAddress = null;
        var cut = RenderComponent<MapPage>();

        await cut.InvokeAsync(() => cut.Instance.OnMapPressed(54.354, 18.656));

        Assert.Contains("54.354, 18.656", cut.Find(".map-press-asks").TextContent);
    }

    /// <summary>A pressed place travels to the editor exactly as a searched one does - see ChosenPlace.</summary>
    [Fact]
    public async Task A_pressed_place_reaches_the_editor_that_makes_something_of_it()
    {
        GrantLocations();
        var chosenPlace = Services.GetRequiredService<ChosenPlace>();
        var cut = RenderComponent<MapPage>();
        await cut.InvokeAsync(() => cut.Instance.OnMapPressed(54.354, 18.656));
        YesTo(cut, ".map-press-asks");

        UseThePlace(cut);
        cut.FindAll(".map-overlay-confirm button")
            .First(button => button.TextContent.Contains("An event in the calendar")).Click();

        var handedOver = chosenPlace.Take();
        Assert.NotNull(handedOver);
        Assert.Equal("Wały Piastowskie 1, Gdańsk", handedOver.Address);
        Assert.Equal(54.354, handedOver.Latitude);
    }

    private static void UseThePlace(IRenderedFragment cut)
        => cut.FindAll(".map-create-event button").First(button => button.TextContent.Contains("Yes, use it")).Click();

    /// <summary>
    /// A share ends from the row it is on, rather than from behind the menu that says how it is made -
    /// ending one is not one of the ways of making one.
    /// </summary>
    [Fact]
    public void A_share_is_ended_from_its_own_row()
    {
        GrantLocations();
        _ownSharesJson = OneShareTo(FriendUserId);
        var cut = RenderComponent<MapPage>();

        cut.Find(".map-share-row .map-share-stop").Click();

        Assert.Contains(
            _deletedPaths,
            path => path.EndsWith($"/location/shares/{FriendUserId}", StringComparison.Ordinal));
    }

    private static void Search(IRenderedFragment cut, string text)
    {
        cut.Find(".map-search-box").Input(text);
        cut.FindAll(".map-search button").First(button => button.TextContent.Contains("Search")).Click();
    }

    /// <summary>
    /// Re-reads the permissions with Location granted. Registered as one instance the page shares, so
    /// granting here is granting for the render that follows.
    /// </summary>
    private void GrantLocations()
    {
        _grantedPermissionsJson = "{\"granted\":[\"Location\"]}";
        Services.GetRequiredService<UserPermissionState>().RefreshAsync().GetAwaiter().GetResult();
    }

    private string _grantedPermissionsJson = "{\"granted\":[]}";

    /// <summary>What Nominatim says is at the point pressed, or null for a spot it knows nothing about.</summary>
    private string? _reverseGeocodedAddress = "Wały Piastowskie 1, Gdańsk";

    /// <summary>Whoever this reader is sharing with in a given test - nobody, unless the test says so.</summary>
    private static readonly Guid FriendUserId = Guid.NewGuid();
    private string _ownSharesJson = "[]";

    /// <summary>What the account already had recorded when the page opened - nothing, unless a test says so.</summary>
    private string _ownLocationJson = "null";

    private static string OwnLocation()
        => "{\"address\":\"Długa 4, Warszawa\",\"latitude\":52.25,\"longitude\":21.0,"
            + "\"recordedAtUtc\":\"2026-08-01T10:00:00+00:00\"}";

    /// <summary>Every DELETE the page made, so a test can say which row it ended rather than that it ended one.</summary>
    private readonly List<string> _deletedPaths = [];

    /// <summary>
    /// One position this reader is sharing. Listed as it comes off the wire - unlike a position shared
    /// *with* somebody, which only opens with a pairwise key no test renderer can make, which is why
    /// the other end of this is covered where the rule itself lives: Orbit.Api.Tests' SharedLocationTests.
    /// </summary>
    private static string OneShareTo(Guid recipientUserId, bool isContinuous = false)
        => "[{\"sharerUserId\":\"" + OwnUserId + "\",\"recipientUserId\":\"" + recipientUserId + "\","
            + "\"ciphertextBase64\":\"\",\"nonceBase64\":\"\",\"isContinuous\":" + (isContinuous ? "true" : "false") + ","
            + "\"updatedAtUtc\":\"2026-08-01T10:00:00+00:00\"}]";

    /// <summary>
    /// A live share left running from a previous visit used to show whatever was last saved until the
    /// timer ticked, up to a minute away - opening the page is what freshens it now, not a wait or a
    /// press on Update.
    /// </summary>
    [Fact]
    public void Opening_the_page_with_a_live_share_already_running_tries_to_freshen_the_position_at_once()
    {
        GrantLocations();
        _ownLocationJson = OwnLocation();
        _ownSharesJson = OneShareTo(FriendUserId, isContinuous: true);

        var cut = RenderComponent<MapPage>();

        // DevicePreferences.AllowLocation defaults to off in this fixture, so the attempt refuses at
        // the same first guard RecordCurrentLocationAsync always would - but reaching that message at
        // all, on the very first render, is what proves a live share tries to freshen itself right away
        // rather than waiting for the once-a-minute timer to get to it.
        Assert.Contains("isn't allowed to use your location", cut.Markup);
    }

    private void RegisterEverythingThePageAsksFor()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/permissions", StringComparison.Ordinal))
            {
                return Text(_grantedPermissionsJson);
            }

            // The other direction: what is at a point somebody pressed. Answers with a street unless the
            // test has said this spot has none - see _reverseGeocodedAddress.
            if (path.EndsWith("/reverse", StringComparison.Ordinal))
            {
                return Text(_reverseGeocodedAddress is null
                    ? "{}"
                    : "{\"display_name\":\"" + _reverseGeocodedAddress + "\"}");
            }

            // Nominatim, which the page reaches through its own client - the query decides the answer so
            // one handler can stand for "found several" and "found nothing" both.
            if (path.EndsWith("/search", StringComparison.Ordinal))
            {
                return Text(request.RequestUri.Query.Contains("Nowhere", StringComparison.OrdinalIgnoreCase)
                    ? "[]"
                    : """
                      [{"lat":"52.25","lon":"21.0","display_name":"Długa 4, Warszawa"},
                       {"lat":"50.06","lon":"19.94","display_name":"Długa 4, Kraków"}]
                      """);
            }

            // Its shares and its contacts. Nothing recorded and nobody shared with, which is what a
            // fresh account looks like and what these tests are not about.
            if (request.Method == HttpMethod.Delete)
            {
                _deletedPaths.Add(path);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }


            if (path.EndsWith("/location/shares", StringComparison.Ordinal))
            {
                return Text(_ownSharesJson);
            }

            if (path.EndsWith("/location/shared-with-me", StringComparison.Ordinal)
                || path.EndsWith("/chat/contacts", StringComparison.Ordinal))
            {
                return Text("[]");
            }

            // The account itself, with whatever location a test has set up for it.
            return Text(
                "{\"id\":\"" + OwnUserId + "\",\"email\":\"owner@example.com\",\"userName\":\"owner\","
                + "\"displayName\":\"Owner\",\"isEmailVerified\":true,\"hasPassword\":true,"
                + "\"isGoogleLinked\":false,\"location\":" + _ownLocationJson + "}");
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var jsRuntime = new StubJSRuntime();
        var authenticationStateProvider = RegisterAuthentication();
        var usersApiClient = new UsersApiClient(httpClient);
        var ownEncryptionKeyProvider = new OwnEncryptionKeyProvider(jsRuntime, usersApiClient, authenticationStateProvider);
        var chatApiClient = new ChatApiClient(httpClient);

        Services.AddSingleton(usersApiClient);
        Services.AddSingleton(chatApiClient);
        Services.AddSingleton(new GeocodingApiClient(httpClient));
        Services.AddSingleton(new SharedLocationSender(usersApiClient, ownEncryptionKeyProvider, jsRuntime));
        Services.AddSingleton(new EncryptedChatMessageSender(
            jsRuntime, ownEncryptionKeyProvider, usersApiClient, chatApiClient));
        Services.AddSingleton(new EncryptedChatMessageReader(usersApiClient, ownEncryptionKeyProvider, jsRuntime));
        Services.AddSingleton(new DevicePreferences(jsRuntime));
        Services.AddSingleton(new GoogleIntegrationAccess(
            usersApiClient, new DevicePreferences(jsRuntime), NullLogger<GoogleIntegrationAccess>.Instance));
        Services.AddSingleton(new UserPermissionState(usersApiClient));
    }

    private OrbitAuthenticationStateProvider RegisterAuthentication()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(CreateUnsignedJwt()).GetAwaiter().GetResult();
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var provider = new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, refreshHttpClient));
        Services.AddSingleton(provider);
        Services.AddSingleton<AuthenticationStateProvider>(provider);
        Services.AddAuthorizationCore();
        return provider;
    }

    private static string CreateUnsignedJwt()
    {
        var payload = $$"""{"sub":"{{OwnUserId}}","email":"owner@example.com","name":"Test Owner"}""";
        return $"{Base64Url("{\"alg\":\"none\"}")}.{Base64Url(payload)}.";
    }

    private static string Base64Url(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static HttpResponseMessage Text(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
