using Orbit.Web.Components;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Notifications;
using Orbit.Contracts.Tasks;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// What reaches the calendar when a task entry makes an event of itself.
///
/// The place is the part that kept going missing: an entry that says where it happens produced an event
/// that did not, so somebody who filled the box in and saved got an appointment with no location and no
/// word about why.
/// </summary>
public sealed class TaskEditorCalendarLocationTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly Guid TaskListId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();

    private CalendarEventDto[] _existingEvents = [];

    private readonly List<CreateCalendarEventRequest> _created = [];
    private readonly List<UpdateCalendarEventRequest> _updated = [];

    public TaskEditorCalendarLocationTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterAuthentication();
        var mapPicker = JSInterop.SetupModule("./js/mapPicker.js");
        mapPicker.SetupVoid("initializeMapPicker", _ => true).SetVoidResult();
        mapPicker.SetupVoid("disposeMapPicker", _ => true).SetVoidResult();
    }

    /// <summary>
    /// A name typed into the box and never pointed at on the map cannot become the event's place, and
    /// this says so on purpose rather than by omission. The calendar keeps places as coordinates with a
    /// label (see EventLocation), and Orbit will not pick coordinates for a name on its own - "Długa 4"
    /// is a real address in a dozen towns, and quietly choosing one would put the appointment in the
    /// wrong place with nothing to show for it. The calendar's own editor refuses the same way.
    ///
    /// What must not happen is that it goes nowhere silently, which is what the editor now says.
    /// </summary>
    [Fact]
    public void A_name_nobody_pointed_at_stays_a_name_and_the_editor_says_so()
    {
        RegisterApiClients(Item("Dentist", location: "Przychodnia, Długa 4"));
        var cut = Render();
        ExpandTheOnlyItem(cut);
        SayWhenItHappens(cut);

        Assert.Contains("Point at this place on the map", cut.Markup);

        Save(cut);

        Assert.Null(Assert.Single(_created).Details.Location);
    }

    /// <summary>
    /// A pin dropped on the map has to arrive as coordinates, not only as the street it reverse-geocoded
    /// to. The calendar's location is coordinates first (see EventLocation) - an address on its own
    /// cannot be shown on a map, and dropping the pin is what somebody did to avoid typing an address.
    /// </summary>
    [Fact]
    public async Task A_pin_reaches_the_event_as_a_real_place()
    {
        RegisterApiClients(Item("Dentist"));
        var cut = Render();
        ExpandTheOnlyItem(cut);
        ClickButtonSaying(cut, "Show map");

        var overlay = cut.FindComponent<Web.Components.LocationPickerOverlay>();
        await cut.InvokeAsync(() => overlay.Instance.OnMapLocationPicked(52.2497, 21.0122));
        cut.FindAll(".map-overlay-confirm button").First(button => button.TextContent.Contains("Yes")).Click();
        SayWhenItHappens(cut);

        Save(cut);

        var location = Assert.Single(_created).Details.Location;
        Assert.NotNull(location);
        Assert.Equal(52.2497, location.Latitude, precision: 4);
        Assert.Equal(21.0122, location.Longitude, precision: 4);
    }

    /// <summary>
    /// Both halves survive together. Somebody who types "the back entrance" and then drops a pin means
    /// both things: the words are what to call it, the pin is where it is - which is exactly the shape
    /// EventLocation already has.
    /// </summary>
    [Fact]
    public async Task A_name_of_your_own_survives_dropping_a_pin_on_top_of_it()
    {
        RegisterApiClients(Item("Dentist", location: "Wejście od podwórza"));
        var cut = Render();
        ExpandTheOnlyItem(cut);
        ClickButtonSaying(cut, "Show map");

        var overlay = cut.FindComponent<Web.Components.LocationPickerOverlay>();
        await cut.InvokeAsync(() => overlay.Instance.OnMapLocationPicked(52.2497, 21.0122));
        cut.FindAll(".map-overlay-confirm button").First(button => button.TextContent.Contains("Yes")).Click();
        SayWhenItHappens(cut);

        Save(cut);

        var location = Assert.Single(_created).Details.Location;
        Assert.NotNull(location);
        Assert.Equal("Wejście od podwórza", location.Address);
        Assert.Equal(52.2497, location.Latitude, precision: 4);
    }

    /// <summary>An entry that says nowhere makes an event that says nowhere, rather than one at Null Island.</summary>
    [Fact]
    public void An_entry_with_no_place_makes_an_event_with_no_place()
    {
        RegisterApiClients(Item("Dentist"));
        var cut = Render();
        ExpandTheOnlyItem(cut);
        SayWhenItHappens(cut);

        Save(cut);

        Assert.Null(Assert.Single(_created).Details.Location);
    }

    private IRenderedComponent<TaskEditor> Render()
        => RenderComponent<TaskEditor>(parameters => parameters.Add(editor => editor.Id, TaskListId));

    private static void ExpandTheOnlyItem(IRenderedComponent<TaskEditor> cut)
        => cut.Find(".editor-item-toggle").Click();

    private static void ClickButtonSaying(IRenderedComponent<TaskEditor> cut, string label)
        => cut.FindAll("button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal)).Click();

    private static void Save(IRenderedComponent<TaskEditor> cut)
        => cut.FindAll("button").First(button => button.TextContent.Contains("Save", StringComparison.Ordinal)).Click();

    /// <summary>
    /// A calendar entry needs a day before it can be saved at all (see WhatIsWrongWithTheItems), so
    /// every test here gives it one - the entry has to be open for the field to exist.
    /// </summary>
    private static void SayWhenItHappens(IRenderedComponent<TaskEditor> cut)
        => cut.FindAll(".date-field-input")
            .First(field => field.GetAttribute("aria-label") == "Starts")
            .Change("09.03.2026");

    private static TaskItemDto Item(
        string description, string location = "", Guid? linkedCalendarEventId = null)
        => new(
            Guid.NewGuid(), description, DueDateUtc: null, IsCompleted: false, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: new TimeOnly(9, 0),
            nameof(Orbit.Core.Tasks.TaskItemKind.Calendar), location, linkedCalendarEventId);

    private void RegisterApiClients(TaskItemDto item)
    {
        var taskList = new TaskDto(
            TaskListId, "Errands", [item], IsCompleted: false, IsGroup: false, IsPrivate: false,
            EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            // What this whole class is about: the event the entry asks the calendar to create.
            if (request.Method == HttpMethod.Post && path == "/api/calendar-events")
            {
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                _created.Add(JsonSerializer.Deserialize<CreateCalendarEventRequest>(
                    body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
                return Ok(Guid.NewGuid());
            }

            // The other half: an entry that already has an event puts it back in step on every save.
            if (request.Method == HttpMethod.Put && path.StartsWith("/api/calendar-events/", StringComparison.Ordinal))
            {
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                _updated.Add(JsonSerializer.Deserialize<UpdateCalendarEventRequest>(
                    body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            // Creating a new list answers with its id, not with a list of lists like the fallback below.
            if (request.Method == HttpMethod.Post && path == "/api/tasks")
            {
                return Ok(Guid.NewGuid());
            }

            if (path.Contains("/notifications", StringComparison.Ordinal))
            {
                return Ok(new NotificationSettingsDto(
                    true, true, true, true, ShowExceptionDetails: false,
                    BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5));
            }

            if (path.Contains("/calendar", StringComparison.Ordinal))
            {
                return Ok(_existingEvents);
            }

            if (path.Contains("/chat", StringComparison.Ordinal))
            {
                return Ok(Array.Empty<object>());
            }

            if (path.StartsWith("/api/share-links", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.EndsWith("/inventory-references", StringComparison.Ordinal))
            {
                return Ok(Array.Empty<object>());
            }

            if (path.EndsWith("/lock", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return path.EndsWith($"/{TaskListId}", StringComparison.Ordinal)
                ? Ok(taskList)
                : Ok(new[] { taskList });
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new ChatApiClient(httpClient));
        Services.AddSingleton(new CalendarApiClient(httpClient));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new PublicShareApiClient(httpClient));
        Services.AddSingleton(new InventoryApiClient(httpClient));
        RegisterGeocoding();
    }

    /// <summary>Nominatim: the forward search finds nothing, the reverse lookup names one street.</summary>
    private void RegisterGeocoding()
        => Services.AddSingleton(new GeocodingApiClient(
            new HttpClient(new StubHttpMessageHandler(request =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        request.RequestUri!.AbsolutePath.Contains("search", StringComparison.Ordinal)
                            ? "[]"
                            : """{"display_name":"Długa 4, Warszawa"}""",
                        Encoding.UTF8,
                        "application/json")
                }))
            {
                BaseAddress = new Uri("https://geocode.test/")
            }));

    private static HttpResponseMessage Ok<T>(T body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private void RegisterAuthentication()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(CreateUnsignedJwt(new Dictionary<string, string>
        {
            ["sub"] = OwnUserId.ToString(),
            ["email"] = "owner@example.com",
            ["name"] = "Test Owner"
        })).GetAwaiter().GetResult();
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, refreshHttpClient));
        Services.AddSingleton(authenticationStateProvider);
        Services.AddSingleton<AuthenticationStateProvider>(authenticationStateProvider);
        Services.AddAuthorizationCore();

        var jsRuntime = JSInterop.JSRuntime;
        var usersApiClient = new UsersApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(usersApiClient);
        var chatApiClient = new ChatApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(chatApiClient);
        Services.AddSingleton(new EncryptedChatMessageSender(
            jsRuntime,
            new OwnEncryptionKeyProvider(jsRuntime, usersApiClient, authenticationStateProvider),
            usersApiClient,
            chatApiClient));
    }

    private static string CreateUnsignedJwt(Dictionary<string, string> claims)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// The same defect seen from the other side, and the damaging half: an entry that already has an
    /// event puts it back in step on every save, and a save that sends no place at all is a save that
    /// erases one. Nobody touched the location - they opened a task list, changed something else, and
    /// pressed Save.
    /// </summary>
    [Fact]
    public void Saving_an_entry_that_already_has_an_event_does_not_wipe_the_events_place()
    {
        _existingEvents = [EventWithAPlace()];
        RegisterApiClients(Item("Dentist", linkedCalendarEventId: EventId));
        var cut = Render();

        Save(cut);

        var location = Assert.Single(_updated).Details.Location;
        Assert.NotNull(location);
        Assert.Equal("Przychodnia, Długa 4", location.Address);
        Assert.Equal(52.23, location.Latitude, precision: 2);
        Assert.Equal(21.01, location.Longitude, precision: 2);
    }

    private static CalendarEventDto EventWithAPlace()
        => new(
            EventId,
            new CalendarEventDetailsDto(
                "Dentist", null, new EventLocationDto("Przychodnia, Długa 4", 52.23, 21.01), null,
                DateTimeOffset.Now.AddDays(1), DateTimeOffset.Now.AddDays(1).AddHours(1),
                IsAllDay: false, null, [], [], "None", "None"),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    /// <summary>
    /// Opened from the map: a new list starts on one calendar entry already standing at the place that
    /// was pointed at. The place is why they came, so it is filled in and the entry is open - what is
    /// missing is the day, which the map cannot know.
    /// </summary>
    [Fact]
    public void A_new_list_opened_from_the_map_starts_at_that_place()
    {
        RegisterApiClients(Item("unused"));
        Services.GetRequiredService<ChosenPlace>().Hold(new PickedPlace("Długa 4, Warszawa", 52.2497, 21.0122));

        var cut = RenderComponent<TaskEditor>();

        var entry = Assert.Single(cut.FindAll(".editor-item-details"));
        Assert.Equal("Długa 4, Warszawa", LocationBoxIn(entry).GetAttribute("value"));
        // A calendar entry, because it is the only kind that has anywhere to be.
        Assert.Equal(
            nameof(Orbit.Core.Tasks.TaskItemKind.Calendar),
            entry.QuerySelector("select")!.GetAttribute("value") ?? SelectedOptionIn(entry));
    }

    /// <summary>
    /// And the pin travels with it, so saving makes an event at that place rather than one labelled
    /// with its name - which is the whole reason the map handed over coordinates at all.
    /// </summary>
    [Fact]
    public void The_pin_from_the_map_reaches_the_event_that_list_creates()
    {
        RegisterApiClients(Item("unused"));
        Services.GetRequiredService<ChosenPlace>().Hold(new PickedPlace("Długa 4, Warszawa", 52.2497, 21.0122));
        var cut = RenderComponent<TaskEditor>();
        cut.Find(".editor-item-details input").Change("Dentist");
        SayWhenItHappens(cut);

        Save(cut);

        var location = Assert.Single(_created).Details.Location;
        Assert.NotNull(location);
        Assert.Equal(52.2497, location.Latitude, precision: 4);
    }

    /// <summary>A new list opened the ordinary way is still empty.</summary>
    [Fact]
    public void A_new_list_opened_without_the_map_starts_empty()
    {
        RegisterApiClients(Item("unused"));

        Assert.Empty(RenderComponent<TaskEditor>().FindAll(".editor-item-details"));
    }

    private static AngleSharp.Dom.IElement LocationBoxIn(AngleSharp.Dom.IElement entry)
        => entry.QuerySelectorAll("input").First(box => box.GetAttribute("placeholder") == "Where this happens");

    private static string SelectedOptionIn(AngleSharp.Dom.IElement entry)
        => entry.QuerySelector("option[selected]")?.GetAttribute("value") ?? "";
}
