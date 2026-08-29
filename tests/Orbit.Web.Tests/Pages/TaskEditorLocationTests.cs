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
/// Where a calendar entry happens, as the deep editor asks for it. The place is stored once: an entry
/// tied to an event has none of its own, and the box is replaced by what the event says rather than
/// offering a second answer that could drift from the first.
/// </summary>
public sealed class TaskEditorLocationTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly Guid TaskListId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();

    public TaskEditorLocationTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterAuthentication();
        // The overlay's map is Leaflet, which is not loaded here; these tests are about what the editor
        // offers and what it does with the answer.
        var mapPicker = JSInterop.SetupModule("./js/mapPicker.js");
        mapPicker.SetupVoid("initializeMapPicker", _ => true).SetVoidResult();
        mapPicker.SetupVoid("disposeMapPicker", _ => true).SetVoidResult();
    }

    [Fact]
    public void A_checklist_entry_is_asked_nothing_about_where_it_happens()
    {
        // It has nowhere to be, so a location box would be a question with no answer.
        RegisterApiClients(Item("Buy milk", kind: "Checklist"));
        var cut = Render();

        ExpandTheOnlyItem(cut);

        Assert.DoesNotContain("Where this happens", cut.Markup);
        Assert.DoesNotContain("Show map", cut.Markup);
    }

    [Fact]
    public void A_calendar_entry_can_be_typed_or_pointed_at()
    {
        RegisterApiClients(Item("Dentist", kind: "Calendar"));
        var cut = Render();

        ExpandTheOnlyItem(cut);

        Assert.Contains("Where this happens", cut.Markup);
        Assert.Contains("Show map", cut.Markup);
    }

    [Fact]
    public void An_entry_tied_to_an_event_is_told_where_it_is_rather_than_asked()
    {
        RegisterApiClients(Item("Dentist", kind: "Calendar", linkedCalendarEventId: EventId));
        var cut = Render();

        ExpandTheOnlyItem(cut);

        Assert.Contains("Przychodnia, Długa 4", cut.Markup);
        Assert.DoesNotContain("Where this happens", cut.Markup);
        // Nothing to point at either: the event holds the place, and a second answer could drift.
        Assert.DoesNotContain("Show map", cut.Markup);
    }

    [Fact]
    public void The_map_opens_over_the_page_rather_than_inside_the_row()
    {
        RegisterApiClients(Item("Dentist", kind: "Calendar"));
        var cut = Render();
        ExpandTheOnlyItem(cut);

        ClickButtonSaying(cut, "Show map");

        Assert.Single(cut.FindAll(".map-overlay"));
        Assert.Contains("Click the map to drop a pin.", cut.Markup);
    }

    [Fact]
    public void A_confirmed_pin_fills_the_location_box_and_closes_the_map()
    {
        RegisterApiClients(Item("Dentist", kind: "Calendar"));
        var cut = Render();
        ExpandTheOnlyItem(cut);
        ClickButtonSaying(cut, "Show map");

        var overlay = cut.FindComponent<Web.Components.LocationPickerOverlay>();
        cut.InvokeAsync(() => overlay.Instance.OnMapLocationPicked(52.2497, 21.0122)).GetAwaiter().GetResult();
        cut.FindAll(".map-overlay-confirm button").First(button => button.TextContent.Contains("Yes")).Click();

        Assert.Empty(cut.FindAll(".map-overlay"));
        Assert.Equal("Długa 4, Warszawa", LocationBoxOf(cut).GetAttribute("value"));
    }

    [Fact]
    public void Backing_out_of_the_map_leaves_what_was_already_typed()
    {
        // A stray click on a map must not rewrite an address somebody typed.
        RegisterApiClients(Item("Dentist", kind: "Calendar", location: "Przychodnia"));
        var cut = Render();
        ExpandTheOnlyItem(cut);
        ClickButtonSaying(cut, "Show map");

        var overlay = cut.FindComponent<Web.Components.LocationPickerOverlay>();
        cut.InvokeAsync(() => overlay.Instance.OnMapLocationPicked(52.2497, 21.0122)).GetAwaiter().GetResult();
        cut.FindAll(".map-overlay-confirm button").First(button => button.TextContent.Contains("Cancel")).Click();

        Assert.Empty(cut.FindAll(".map-overlay"));
        Assert.Equal("Przychodnia", LocationBoxOf(cut).GetAttribute("value"));
    }

    private IRenderedComponent<TaskEditor> Render()
        => RenderComponent<TaskEditor>(parameters => parameters.Add(editor => editor.Id, TaskListId));

    private static void ExpandTheOnlyItem(IRenderedComponent<TaskEditor> cut)
        => cut.Find(".editor-item-toggle").Click();

    private static void ClickButtonSaying(IRenderedComponent<TaskEditor> cut, string label)
        => cut.FindAll("button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal)).Click();

    private static AngleSharp.Dom.IElement LocationBoxOf(IRenderedComponent<TaskEditor> cut)
        => cut.FindAll(".editor-item-details input")
            .First(box => box.GetAttribute("placeholder") == "Where this happens");

    private static TaskItemDto Item(
        string description, string kind, string location = "", Guid? linkedCalendarEventId = null)
        => new(
            Guid.NewGuid(), description, DueDateUtc: null, IsCompleted: false, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: new TimeOnly(9, 0),
            kind, location, linkedCalendarEventId);

    private void RegisterApiClients(TaskItemDto item)
    {
        var taskList = new TaskDto(
            TaskListId, "Errands", [item], IsCompleted: false, IsGroup: false, IsPrivate: false,
            EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("/notifications", StringComparison.Ordinal))
            {
                return Ok(new NotificationSettingsDto(
                    true, true, true, true, ShowExceptionDetails: false,
                    BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5));
            }

            if (path.Contains("/calendar", StringComparison.Ordinal))
            {
                return Ok(new[] { CalendarEvent() });
            }

            if (path.Contains("/chat", StringComparison.Ordinal))
            {
                return Ok(Array.Empty<object>());
            }

            // ShareLinkButton asks on render whether this list already has a public link; it has none.
            if (path.StartsWith("/api/share-links", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            // The editor takes the edit lock as it opens; nobody else holds it here.
            if (path.EndsWith("/lock", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            // Both the list of lists and the one being edited come from here; the editor asks for the
            // list by id after the collection, and either answer is a task list it can read.
            return path.EndsWith($"/{TaskListId}", StringComparison.Ordinal)
                ? Ok(taskList)
                : Ok(new[] { taskList });
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new TasksApiClient(httpClient));
        // After the one the authentication setup registered, so this stubbed one is what the page resolves.
        Services.AddSingleton(new ChatApiClient(httpClient));
        Services.AddSingleton(new CalendarApiClient(httpClient));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new PublicShareApiClient(httpClient));
        RegisterGeocoding();
    }

    private static CalendarEventDto CalendarEvent()
        => new(
            EventId,
            new CalendarEventDetailsDto(
                "Dentist", null, new EventLocationDto("Przychodnia, Długa 4", 52.23, 21.01), null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
                IsAllDay: false, null, [], [], "None", "None"),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

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
}
