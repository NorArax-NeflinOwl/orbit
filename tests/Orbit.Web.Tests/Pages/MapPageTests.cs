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
        Assert.Contains("A place worth keeping", asked);
        Assert.Contains("An event in the calendar", asked);
        Assert.Contains("A task list starting here", asked);
    }

    /// <summary>
    /// And a place is what the picker opens on. It is the least somebody can mean by pressing a map -
    /// the other two ask for a time or a job nobody has mentioned - and it is the answer that was not
    /// possible at all until places existed.
    /// </summary>
    [Fact]
    public void A_place_is_what_the_question_opens_on()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();
        Search(cut, "Długa 4");

        UseThePlace(cut);

        Assert.Equal("Place", cut.Find("#mapWhatHappensHere").GetAttribute("value"));
    }

    /// <summary>
    /// Answering "a place worth keeping" opens the form on the pin, with the address already in it -
    /// the whole point of having pressed the map rather than the + in its corner.
    /// </summary>
    [Fact]
    public void Keeping_the_place_opens_the_form_on_that_pin()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();
        Search(cut, "Długa 4");
        UseThePlace(cut);

        MakeItA(cut, "Place");

        Assert.Equal("Długa 4, Warszawa", cut.Find("#placeFormWhere").GetAttribute("value"));
    }

    /// <summary>
    /// The + in the map's corner opens the same form with nothing in it - deliberately not seeded with
    /// whatever pin happens to be on the map, since somebody who meant that pin has the question about
    /// it in front of them already.
    /// </summary>
    [Fact]
    public void The_plus_on_the_map_opens_an_empty_form()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();
        Search(cut, "Długa 4");

        cut.Find(".map-add-place-button").Click();

        Assert.Equal(string.Empty, cut.Find("#placeFormWhere").GetAttribute("value"));
        Assert.Equal(string.Empty, cut.Find("#placeFormName").GetAttribute("value"));
    }

    /// <summary>
    /// A place is not an appointment: the answer hands the pin to the editor that makes something of it
    /// rather than writing an event nobody has said when is - see ChosenPlace.
    /// </summary>
    [Theory]
    [InlineData("Event", "/calendar/new")]
    [InlineData("TaskList", "/tasks/new")]
    public void The_answer_hands_the_pin_to_the_editor_that_makes_it(string answer, string url)
    {
        GrantLocations();
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var chosenPlace = Services.GetRequiredService<ChosenPlace>();
        var cut = RenderComponent<MapPage>();
        Search(cut, "Długa 4");
        UseThePlace(cut);

        MakeItA(cut, answer);

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
        MakeItA(cut, "Event");

        var handedOver = chosenPlace.Take();
        Assert.NotNull(handedOver);
        Assert.Equal("Wały Piastowskie 1, Gdańsk", handedOver.Address);
        Assert.Equal(54.354, handedOver.Latitude);
    }

    private static void UseThePlace(IRenderedFragment cut)
        => cut.FindAll(".map-create-event button").First(button => button.TextContent.Contains("Yes, use it")).Click();

    /// <summary>
    /// Answers "what happens here?" the way somebody does: choose on the picker, then press Create. The
    /// value is the kind's own name rather than its label, because that is what the option carries -
    /// see MapPage's PlanForAPlace.
    /// </summary>
    private static void MakeItA(IRenderedFragment cut, string kind)
    {
        cut.Find("#mapWhatHappensHere").Change(kind);
        cut.FindAll(".map-overlay-confirm button").First(button => button.TextContent.Contains("Create")).Click();
    }

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
    /// The reader's calendar and their lists - empty unless a test puts something in them. Written as
    /// JSON rather than built from the DTOs because the whole file answers the wire this way, and a page
    /// reading a field it was never sent is exactly what a hand-built DTO would hide.
    /// </summary>
    private string _calendarEventsJson = "[]";
    private string _taskListsJson = "[]";

    private static readonly Guid PlacedEventId = Guid.NewGuid();
    private static readonly Guid TaskListId = Guid.NewGuid();
    private static readonly Guid TaskItemId = Guid.NewGuid();

    /// <summary>
    /// One appointment that says where it happens. <paramref name="startsInDays"/> decides whether it is
    /// ahead or behind - the panel leaves the past out until it is asked for it.
    /// </summary>
    private static string OneEventAtAPlace(string title, int startsInDays, bool hasAPlace = true)
    {
        var start = DateTimeOffset.UtcNow.AddDays(startsInDays);
        var location = hasAPlace
            ? "{\"address\":\"Wały Piastowskie 1, Gdańsk\",\"latitude\":54.35,\"longitude\":18.65}"
            : "null";
        return "[{\"id\":\"" + PlacedEventId + "\",\"details\":{\"title\":\"" + title + "\","
            + "\"description\":null,\"location\":" + location + ",\"color\":\"#ff8800\","
            + "\"startUtc\":\"" + start.ToString("O") + "\",\"endUtc\":\"" + start.AddHours(1).ToString("O") + "\","
            + "\"isAllDay\":false,\"recurrence\":null,\"guests\":[],\"reminderMinutesBeforeStart\":[],"
            + "\"reminderNotificationChannel\":\"None\",\"priority\":\"Normal\",\"notifyAtStart\":false},"
            + "\"createdAtUtc\":\"2026-08-01T10:00:00+00:00\",\"updatedAtUtc\":\"2026-08-01T10:00:00+00:00\","
            + "\"isShared\":false,\"sharedByUserName\":null,\"accessLevel\":\"CanEdit\",\"originalOwnerUserId\":null}]";
    }

    /// <summary>One list whose entry raised the appointment above - see CalendarEventDestination.</summary>
    private static string OneListWhoseEntryRaisedTheEvent(string listTitle, string entryTitle, bool isEntryDone = false)
        => "[{\"id\":\"" + TaskListId + "\",\"title\":\"" + listTitle + "\",\"items\":["
            + "{\"id\":\"" + TaskItemId + "\",\"description\":\"" + entryTitle + "\",\"dueDateUtc\":null,"
            + "\"isCompleted\":" + (isEntryDone ? "true" : "false") + ",\"linkedTaskListId\":null,\"overdueNotificationChannel\":\"None\","
            + "\"remindDaily\":false,\"dailyReminderNotificationChannel\":\"None\","
            + "\"dailyReminderTimeOfDay\":\"09:00:00\",\"kind\":\"Calendar\",\"location\":\"\","
            + "\"linkedCalendarEventId\":\"" + PlacedEventId + "\"}],"
            + "\"isCompleted\":false,\"isGroup\":false,\"isPrivate\":false,\"encryptedContent\":null,"
            + "\"createdAtUtc\":\"2026-08-01T10:00:00+00:00\",\"updatedAtUtc\":\"2026-08-01T10:00:00+00:00\","
            + "\"isShared\":false,\"sharedByUserName\":null,\"accessLevel\":\"CanEdit\","
            + "\"originalOwnerUserId\":null}]";

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

    /// <summary>
    /// The map used to know where people were and nothing about where the reader was going. An
    /// appointment that says where it happens is a row in the panel and a pin beside the rest.
    /// </summary>
    [Fact]
    public void An_appointment_that_says_where_it_happens_is_listed()
    {
        GrantLocations();
        _calendarEventsJson = OneEventAtAPlace("Dentist", startsInDays: 2);

        var cut = RenderComponent<MapPage>();

        Assert.Contains(PlaceRows(cut), row => row.TextContent.Contains("Dentist", StringComparison.Ordinal));
    }

    /// <summary>An appointment with no address is not a place, and nothing on this page can draw it.</summary>
    [Fact]
    public void An_appointment_with_no_address_is_not_listed()
    {
        GrantLocations();
        _calendarEventsJson = OneEventAtAPlace("A call", startsInDays: 2, hasAPlace: false);

        var cut = RenderComponent<MapPage>();

        Assert.Empty(PlaceRows(cut));
    }

    /// <summary>
    /// An appointment a list raised is named for that list and opens as its entry - the same rule the
    /// calendar and the dashboard follow, see CalendarEventDestination. It also comes back here
    /// afterwards, which is what ReturnTo is for.
    /// </summary>
    [Fact]
    public void An_appointment_a_list_raised_is_named_for_it_and_opens_as_its_entry()
    {
        GrantLocations();
        _calendarEventsJson = OneEventAtAPlace("Pick up the keys", startsInDays: 1);
        _taskListsJson = OneListWhoseEntryRaisedTheEvent("Moving", "Pick up the keys");

        var cut = RenderComponent<MapPage>();

        var row = Assert.Single(PlaceRows(cut));
        Assert.Contains("Moving: Pick up the keys", row.TextContent);
        Assert.Equal(
            $"/tasks/{TaskListId}/items/{TaskItemId}?returnTo=%2Fmap",
            row.QuerySelector("a")!.GetAttribute("href"));
    }

    /// <summary>An appointment of its own opens the event itself, and comes back here too.</summary>
    [Fact]
    public void An_appointment_of_its_own_opens_the_event()
    {
        GrantLocations();
        _calendarEventsJson = OneEventAtAPlace("Dentist", startsInDays: 2);

        var cut = RenderComponent<MapPage>();

        Assert.Equal(
            $"/calendar/{PlacedEventId}?returnTo=%2Fmap",
            Assert.Single(PlaceRows(cut)).QuerySelector("a")!.GetAttribute("href"));
    }

    /// <summary>
    /// A map is about where somebody is going, so what has already happened is off until it is asked
    /// for - and then it is there, rather than gone for good.
    /// </summary>
    [Fact]
    public void What_has_already_happened_is_left_out_until_the_menu_asks_for_it()
    {
        GrantLocations();
        _calendarEventsJson = OneEventAtAPlace("Last week's dentist", startsInDays: -7);

        var cut = RenderComponent<MapPage>();
        Assert.Empty(PlaceRows(cut));

        cut.Find(".overflow-menu-trigger").Click();
        ButtonSaying(cut, "Show places already past").Click();

        Assert.Contains(PlaceRows(cut), row => row.TextContent.Contains("Last week's dentist", StringComparison.Ordinal));
    }

    /// <summary>
    /// An appointment a list raised is done when its entry is ticked off, whatever the clock says - the
    /// shopping was done on Tuesday for a slot booked on Friday. The map went on drawing a pin for it
    /// until Friday came and went, which is a pin for somewhere nobody is going.
    /// </summary>
    [Fact]
    public void An_appointment_whose_entry_is_ticked_off_is_behind_you_before_its_time_comes()
    {
        GrantLocations();
        _calendarEventsJson = OneEventAtAPlace("Pick up the keys", startsInDays: 3);
        _taskListsJson = OneListWhoseEntryRaisedTheEvent("Moving", "Pick up the keys", isEntryDone: true);

        var cut = RenderComponent<MapPage>();
        Assert.Empty(PlaceRows(cut));

        // And it is there when the past is asked for, rather than gone for good.
        ShowPastPlaces(cut);

        Assert.Contains(PlaceRows(cut), row => row.TextContent.Contains("Pick up the keys", StringComparison.Ordinal));
    }

    /// <summary>
    /// How far back "show me the past" goes. Everything until a day is named, because that is what the
    /// option meant before there was anywhere to say otherwise - and on an account with a year of
    /// appointments in it, that answer buried the two the reader wanted.
    /// </summary>
    [Fact]
    public void The_past_can_be_asked_for_from_a_day_rather_than_from_the_beginning()
    {
        GrantLocations();
        _calendarEventsJson = OneEventAtAPlace("Last week's dentist", startsInDays: -7);

        var cut = RenderComponent<MapPage>();
        ShowPastPlaces(cut);
        Assert.Single(PlaceRows(cut));

        cut.Find("#mapPastFrom").Change(DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd"));

        Assert.Empty(PlaceRows(cut));
    }

    /// <summary>
    /// And both halves of it are remembered on this device. They were held by the page alone, so a
    /// reader who asked for the past had to find the option and press it again on every visit - and the
    /// day they narrowed it to went with it. See MapPinVisibility, which keeps the eyes beside them.
    /// </summary>
    [Fact]
    public void Asking_for_the_past_is_remembered_for_the_next_visit()
    {
        GrantLocations();
        _calendarEventsJson = OneEventAtAPlace("Last week's dentist", startsInDays: -7);
        var cut = RenderComponent<MapPage>();

        ShowPastPlaces(cut);
        cut.Find("#mapPastFrom").Change(DateTime.Today.AddDays(-10).ToString("yyyy-MM-dd"));

        // The same browser, the page opened again: the stub's localStorage is what carries it over.
        var reopened = RenderComponent<MapPage>();

        Assert.Single(PlaceRows(reopened));
        Assert.Equal(
            DateTime.Today.AddDays(-10).ToString("yyyy-MM-dd"), reopened.Find("#mapPastFrom").GetAttribute("value"));
    }

    /// <summary>And the box is only there while the past is being shown, being a question about the past.</summary>
    [Fact]
    public void The_day_to_show_from_is_only_asked_while_the_past_is_shown()
    {
        GrantLocations();

        var cut = RenderComponent<MapPage>();
        Assert.Empty(cut.FindAll("#mapPastFrom"));

        ShowPastPlaces(cut);

        Assert.Single(cut.FindAll("#mapPastFrom"));
    }

    /// <summary>
    /// The eye takes a list's pins off the map without taking the list off the page - so there is still
    /// something to press to get them back, and the reader can still read what they hid.
    /// </summary>
    [Fact]
    public void Hiding_a_lists_pins_leaves_the_list_where_it_is()
    {
        GrantLocations();
        _calendarEventsJson = OneEventAtAPlace("Dentist", startsInDays: 2);
        var cut = RenderComponent<MapPage>();

        PinToggleFor(cut, "Where your plans are").Click();

        Assert.Single(PlaceRows(cut));
        Assert.Contains("off", PinToggleFor(cut, "Where your plans are").ClassName);
    }

    /// <summary>
    /// And it is not undone by asking for the past. Somebody who hid their plans and then asked to see
    /// past ones meant to be shown nothing, not to have the whole lot come back.
    /// </summary>
    [Fact]
    public void Asking_for_the_past_does_not_bring_hidden_pins_back()
    {
        GrantLocations();
        _calendarEventsJson = OneEventAtAPlace("Dentist", startsInDays: 2);
        var cut = RenderComponent<MapPage>();
        PinToggleFor(cut, "Where your plans are").Click();

        ShowPastPlaces(cut);

        Assert.Contains("off", PinToggleFor(cut, "Where your plans are").ClassName);
    }

    /// <summary>Both lists have one, and each answers for its own pins.</summary>
    [Fact]
    public void Each_list_has_its_own_eye()
    {
        GrantLocations();

        var cut = RenderComponent<MapPage>();

        PinToggleFor(cut, "Sharing with you").Click();

        Assert.Contains("off", PinToggleFor(cut, "Sharing with you").ClassName);
        Assert.DoesNotContain("off", PinToggleFor(cut, "Where your plans are").ClassName);
    }

    private static void ShowPastPlaces(IRenderedFragment cut)
    {
        cut.Find(".overflow-menu-trigger").Click();
        ButtonSaying(cut, "Show places already past").Click();
    }

    /// <summary>
    /// The places kept for their own sake get a list of their own, not a share of the plans: a plan is
    /// something happening at a time, and these have no time at all - which is the whole point of them.
    /// </summary>
    [Fact]
    public void A_kept_place_is_listed_under_its_own_heading()
    {
        GrantLocations();
        _placesJson = OneKeptPlace("The good bakery");
        var cut = RenderComponent<MapPage>();

        var section = SectionNamed(cut, "Places you keep");
        Assert.Contains("The good bakery", section.TextContent, StringComparison.Ordinal);
    }

    /// <summary>And it says so rather than drawing an empty box on an account that has kept none.</summary>
    [Fact]
    public void With_nothing_kept_the_list_says_where_one_starts()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();

        Assert.Contains(
            "Nothing kept yet",
            SectionNamed(cut, "Places you keep").TextContent,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The eye works the way every other one on this page does: it takes the list's pins off the map
    /// without taking the list off the page.
    /// </summary>
    [Fact]
    public void The_eye_on_the_places_takes_their_pins_off_the_map_and_leaves_the_list()
    {
        GrantLocations();
        _placesJson = OneKeptPlace("The good bakery");
        var cut = RenderComponent<MapPage>();

        PinToggleFor(cut, "Places you keep").Click();

        Assert.Contains("off", PinToggleFor(cut, "Places you keep").ClassName);
        Assert.Contains(
            "The good bakery", SectionNamed(cut, "Places you keep").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pin, name, eye - the order every card in Orbit puts them in, so a panel on the map reads like a
    /// card anywhere else. It was pin-then-eye at the right-hand end for a day, which read as two
    /// answers to one question.
    /// </summary>
    [Fact]
    public void A_panel_heading_reads_pin_then_name_then_eye()
    {
        GrantLocations();
        var heading = SectionNamed(cut: RenderComponent<MapPage>(), heading: "Where your plans are")
            .QuerySelector(".map-panel-heading-row")!;

        var order = heading.Children.Select(child => child.ClassList.Contains("pin-button") ? "pin"
            : child.ClassList.Contains("map-pin-toggle") ? "eye"
            : "name").ToList();

        Assert.Equal(["pin", "name", "eye"], order);
    }

    /// <summary>The section whose heading says this.</summary>
    private static AngleSharp.Dom.IElement SectionNamed(IRenderedFragment cut, string heading)
        => cut.FindAll(".map-panel-section")
            .First(section => section.QuerySelector(".map-panel-heading")?.TextContent.Contains(heading, StringComparison.Ordinal) == true);

    /// <summary>
    /// The panel holds three lists and the one that matters is a question about the day rather than
    /// about Orbit - somebody meeting a person wants the names, somebody on their way somewhere wants
    /// the plans, and on a phone the third of them is a scroll away. Pinning brings one to the top.
    ///
    /// Checked as the class that lifts it rather than as a position in the document: the lift is
    /// `order`, so the boxes are drawn where they were written and the browser puts them elsewhere -
    /// which is a thing bUnit's own tree cannot see.
    /// </summary>
    [Fact]
    public void A_panel_list_can_be_pinned_to_the_top_of_the_panel()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();
        Assert.Empty(cut.FindAll(".map-panel-section-pinned"));

        PanelPinFor(cut, "Where your plans are").Click();

        var pinned = Assert.Single(cut.FindAll(".map-panel-section-pinned"));
        Assert.Contains("Where your plans are", pinned.TextContent, StringComparison.Ordinal);
    }

    /// <summary>Several may be pinned at once, and pressing one again puts it back.</summary>
    [Fact]
    public void Pinning_a_second_list_leaves_the_first_pinned_and_unpinning_puts_it_back()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();
        PanelPinFor(cut, "Where your plans are").Click();
        PanelPinFor(cut, "Sharing with you").Click();
        Assert.Equal(2, cut.FindAll(".map-panel-section-pinned").Count);

        PanelPinFor(cut, "Where your plans are").Click();

        var pinned = Assert.Single(cut.FindAll(".map-panel-section-pinned"));
        Assert.Contains("Sharing with you", pinned.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// The eye and the pin are two questions about one list - what the map draws, and what the reader
    /// wants in front of them - so pressing one must not answer the other.
    /// </summary>
    [Fact]
    public void Pinning_a_list_does_not_take_its_pins_off_the_map()
    {
        GrantLocations();
        var cut = RenderComponent<MapPage>();

        PanelPinFor(cut, "Sharing with you").Click();

        Assert.Equal("true", PinToggleFor(cut, "Sharing with you").GetAttribute("aria-pressed"));
    }

    /// <summary>
    /// The places this account keeps - see Orbit.Core.Places.Place. Empty unless a test sets it, which
    /// is what a reader who has never kept one looks like.
    /// </summary>
    private string _placesJson = "[]";

    /// <summary>One kept place, as the server sends it.</summary>
    private static string OneKeptPlace(string name, string colour = "", string priority = "Normal")
        => "[{\"id\":\"" + Guid.NewGuid() + "\",\"name\":\"" + name + "\",\"description\":\"\","
        + "\"where\":{\"address\":\"Piękna 1\",\"latitude\":52.2,\"longitude\":21.0},"
        + "\"colour\":\"" + colour + "\",\"priority\":\"" + priority + "\",\"taskListIds\":[],"
        + "\"createdAtUtc\":\"2026-09-10T10:00:00+00:00\",\"updatedAtUtc\":\"2026-09-10T10:00:00+00:00\"}]";

    /// <summary>The pin on the heading of the section named this - see MapPanelPins.</summary>
    private static AngleSharp.Dom.IElement PanelPinFor(IRenderedFragment cut, string heading)
        => cut.FindAll(".map-panel-section")
            .First(section => section.QuerySelector(".map-panel-heading")?.TextContent.Contains(heading, StringComparison.Ordinal) == true)
            .QuerySelector(".pin-button")!;

    /// <summary>The eye on the heading of the section named this - see MapPinVisibility.</summary>
    private static AngleSharp.Dom.IElement PinToggleFor(IRenderedFragment cut, string heading)
        => cut.FindAll(".map-panel-section")
            .First(section => section.QuerySelector(".map-panel-heading")?.TextContent.Contains(heading, StringComparison.Ordinal) == true)
            .QuerySelector(".map-pin-toggle")!;

    /// <summary>The rows of the "Where your plans are" section, which is the last one in the panel.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> PlaceRows(IRenderedFragment cut)
        => [.. cut.FindAll(".map-panel-section").Last().QuerySelectorAll(".map-share-row")];

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

            // What the reader has planned. Nothing at all unless a test has said otherwise, which is
            // what an account with an empty calendar looks like.
            if (path.EndsWith("/calendar-events", StringComparison.Ordinal))
            {
                return Text(_calendarEventsJson);
            }

            if (path.EndsWith("/tasks", StringComparison.Ordinal))
            {
                return Text(_taskListsJson);
            }

            // The places this account keeps. None unless a test says otherwise, which is what a reader
            // who has never kept one looks like.
            if (path.EndsWith("/places", StringComparison.Ordinal))
            {
                return Text(_placesJson);
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
        Services.AddSingleton(new PlacesApiClient(httpClient));
        Services.AddSingleton(new GeocodingApiClient(httpClient));
        Services.AddSingleton(new CalendarApiClient(httpClient));
        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new SharedLocationSender(usersApiClient, ownEncryptionKeyProvider, jsRuntime));
        Services.AddSingleton(new EncryptedChatMessageSender(
            jsRuntime, ownEncryptionKeyProvider, usersApiClient, chatApiClient));
        Services.AddSingleton(new EncryptedChatMessageReader(usersApiClient, ownEncryptionKeyProvider, jsRuntime));
        Services.AddSingleton(new DevicePreferences(jsRuntime));
        // Which groups of pins the map draws. The stub runtime answers localStorage with null, so both
        // groups are shown - which is what a browser nobody has hidden anything on looks like.
        Services.AddSingleton(new MapPinVisibility(jsRuntime));
        // And which of the panel's lists is kept at the top of it. Same stub, same answer: nothing is
        // pinned, which leaves the panel in the order the page writes it.
        Services.AddSingleton(new MapPanelPins(jsRuntime));
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
