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
using Microsoft.JSInterop;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Notifications;
using Orbit.Contracts.Tasks;
using Orbit.Contracts.Users;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

public sealed class CalendarEventEditorTests : OrbitTestContext
{
    private static readonly Guid ContactUserId = Guid.NewGuid();
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly ContactDto Contact =
        new(ContactUserId, "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false);

    public CalendarEventEditorTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterGoogleIntegrationAccess();
        // One transport for the two clients that write: creating the event answers with an id, and
        // putting it on a list is recorded so a test can say which list it went on. Which lists exist
        // is a field, because most of these tests want none and the picker then does not render.
        var writes = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/items/calendar-event", StringComparison.Ordinal))
            {
                _linkedToPath = path;
                _linkedEventId = ReadTheLinkedEventId(request);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.EndsWith("/api/calendar-events", StringComparison.Ordinal))
            {
                return JsonResponse(SavedEventId);
            }

            if (_existingEvent is { } existingEvent && path == $"/api/calendar-events/{existingEvent.Id}")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(existingEvent) };
            }

            // The edit lock an existing event takes on opening - nobody else holds it here.
            if (path.EndsWith("/lock", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_taskListsJson, Encoding.UTF8, "application/json")
            };
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new CalendarApiClient(writes));
        Services.AddSingleton(new TasksApiClient(writes));
        Services.AddSingleton(new GeocodingApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") }));
        // Only an existing event draws the share link, and only to ask whether one exists yet.
        Services.AddSingleton(new PublicShareApiClient(new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)))
        { BaseAddress = new Uri("https://example.test/") }));
        // CalendarEventEditor.razor fetches notification settings on init - a real (if unreachable)
        // HttpClient like CalendarApiClient/GeocodingApiClient above use would work too (the call is
        // caught and logged, not fatal), but the actual DNS/connect attempt takes real wall-clock time
        // bUnit's synchronous RenderComponent doesn't reliably wait out, unlike a StubHttpMessageHandler's
        // instant in-memory response.
        Services.AddSingleton(new NotificationsApiClient(new HttpClient(
            new StubHttpMessageHandler(_ => JsonResponse(new NotificationSettingsDto(true, true, true, true, true, BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5))))
        { BaseAddress = new Uri("https://example.test/") }));

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
        // Registered under both the concrete type and the base type it derives from, mirroring
        // Program.cs, so components that inject either one resolve to the same instance.
        Services.AddSingleton(authenticationStateProvider);
        Services.AddSingleton<AuthenticationStateProvider>(authenticationStateProvider);
        Services.AddAuthorizationCore();

        // EncryptedChatMessageSender is only exercised by SaveAsync, which none of these tests reach -
        // it just needs to satisfy CalendarEventEditor's @inject, so its own collaborators are wired
        // with the same dummy-HttpClient pattern used above rather than anything meant to actually run.
        // JSInterop.JSRuntime (bUnit's own JS interop double), not Services.GetRequiredService<IJSRuntime>() -
        // resolving a service from Services here would lock the container against further registrations
        // below, since bUnit treats that as "the component tree has started rendering".
        var jsRuntime = JSInterop.JSRuntime;
        var usersApiClient = new UsersApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(usersApiClient);
        var ownEncryptionKeyProvider = new OwnEncryptionKeyProvider(jsRuntime, usersApiClient, authenticationStateProvider);
        var chatApiClientForSender = new ChatApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(new EncryptedChatMessageSender(jsRuntime, ownEncryptionKeyProvider, usersApiClient, chatApiClientForSender));
    }

    /// <summary>
    /// Builds a JWT with a real header and payload but a dummy signature - enough to exercise the
    /// client's own claim-parsing logic, which never checks the signature (the server already did, on
    /// every API call that carries this token).
    /// </summary>
    private static string CreateUnsignedJwt(Dictionary<string, string> claims)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public void Picking_a_contact_adds_them_to_the_guest_list_by_login()
    {
        RegisterChatApiClient([Contact]);

        var cut = RenderComponent<CalendarEventEditor>();
        cut.Find("#guestContactSelect").Change(ContactUserId.ToString());
        cut.Find("#addGuestFromContactButton").Click();

        Assert.Contains("anna", cut.Find("#guestList").TextContent);
    }

    [Fact]
    public void Picking_the_same_contact_twice_does_not_duplicate_the_guest()
    {
        RegisterChatApiClient([Contact]);

        var cut = RenderComponent<CalendarEventEditor>();
        cut.Find("#guestContactSelect").Change(ContactUserId.ToString());
        cut.Find("#addGuestFromContactButton").Click();
        cut.Find("#guestContactSelect").Change(ContactUserId.ToString());
        cut.Find("#addGuestFromContactButton").Click();

        Assert.Single(cut.Find("#guestList").Children);
    }

    [Fact]
    public void Removing_a_guest_takes_them_off_the_list()
    {
        RegisterChatApiClient([Contact]);

        var cut = RenderComponent<CalendarEventEditor>();
        cut.Find("#guestContactSelect").Change(ContactUserId.ToString());
        cut.Find("#addGuestFromContactButton").Click();
        cut.Find("#guestList button").Click();

        Assert.Empty(cut.FindAll("#guestList"));
        Assert.Contains("No guests", cut.Markup);
    }

    [Fact]
    public void A_user_with_no_contacts_sees_an_explanatory_message_instead_of_the_picker()
    {
        RegisterChatApiClient([]);

        var cut = RenderComponent<CalendarEventEditor>();

        Assert.Contains("No contacts", cut.Markup);
        Assert.Empty(cut.FindAll("#guestContactSelect"));
    }

    [Fact]
    public void A_new_event_offers_no_delete()
    {
        // There is nothing to delete yet, and offering it would only lead to a request for an id that
        // does not exist.
        RegisterChatApiClient([]);

        var cut = RenderComponent<CalendarEventEditor>();

        Assert.DoesNotContain("Delete event", cut.Markup);
    }

    [Fact]
    public void A_new_event_arrives_with_a_reminder_that_will_actually_fire()
    {
        RegisterChatApiClient([]);

        var cut = RenderComponent<CalendarEventEditor>();

        // A new event used to arrive with no reminder and both channels set to None, so creating one
        // without opening either dropdown could never notify anybody - which reads, fairly, as event
        // reminders being broken. Tasks and inventory items have defaulted to Push all along.
        var reminderSelect = cut.Find(".reminder-row select");
        Assert.Equal("10", reminderSelect.GetAttribute("value"));

        var reminderChannel = cut.Find("#eventReminderChannel");
        Assert.Equal("Push", reminderChannel.GetAttribute("value"));
    }

    [Fact]
    public void Being_told_when_the_event_begins_is_asked_for_as_its_own_question()
    {
        RegisterChatApiClient([]);

        var cut = RenderComponent<CalendarEventEditor>();

        // It used to be a zero-minute entry in the reminder picker. It is a checkbox now: "tell me it
        // has started" is a different question from "tell me it is coming", and a reminders table with
        // a "0 minutes before" row in it reads as a mistake. Off by default - see EventFormModel.
        var checkbox = cut.FindAll("input[type=checkbox]")
            .Single(box => box.ParentElement!.TextContent.Contains("Also when it starts"));
        Assert.False(checkbox.HasAttribute("checked"));
    }

    /// <summary>The id a created event comes back with, so a test can say the link points at that event.</summary>
    private static readonly Guid SavedEventId = Guid.NewGuid();

    /// <summary>What the picker is offered. Empty for every test that is not about it.</summary>
    private string _taskListsJson = "[]";

    /// <summary>Where the event was put, and which event it was - null until something puts it somewhere.</summary>
    private string? _linkedToPath;
    private Guid? _linkedEventId;

    /// <summary>The event this editor is opened on, when a test opens one rather than starting a new one.</summary>
    private CalendarEventDto? _existingEvent;

    private static Guid ReadTheLinkedEventId(HttpRequestMessage request)
    {
        var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        return JsonSerializer.Deserialize<LinkCalendarEventRequest>(
            body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.CalendarEventId;
    }

    /// <summary>
    /// A list the picker can offer: not private, and not held through a read-only share - the two the
    /// editor narrows by, because either would be refused by the server.
    /// </summary>
    private static string OneTaskListCalled(Guid id, string title, Guid? holdingAnEntryFor = null)
        => "[{\"id\":\"" + id + "\",\"title\":\"" + title + "\",\"items\":[" + EntryFor(holdingAnEntryFor) + "],\"isCompleted\":false,"
            + "\"isGroup\":false,\"isPrivate\":false,\"encryptedContent\":null,"
            + "\"createdAtUtc\":\"2026-08-01T10:00:00+00:00\",\"updatedAtUtc\":\"2026-08-01T10:00:00+00:00\","
            + "\"isShared\":false,\"sharedByUserName\":null,\"accessLevel\":\"CanEdit\","
            + "\"originalOwnerUserId\":null,\"description\":\"\"}]";

    /// <summary>An entry of the kind LinkCalendarEventToTaskListCommand appends: it points at an event and holds no copy of it.</summary>
    private static string EntryFor(Guid? calendarEventId)
        => calendarEventId is not { } eventId
            ? string.Empty
            : "{\"id\":\"" + Guid.NewGuid() + "\",\"description\":\"Dentist\",\"isCompleted\":false,"
                + "\"dueDateUtc\":null,\"kind\":\"Calendar\",\"linkedCalendarEventId\":\"" + eventId + "\"}";

    /// <summary>
    /// An event can be put on a task list from here, which gives the list an entry pointing at the
    /// event rather than a copy of it - see LinkCalendarEventToTaskListCommand.
    /// </summary>
    [Fact]
    public void An_event_can_be_put_on_a_task_list_as_it_is_saved()
    {
        var taskListId = Guid.NewGuid();
        _taskListsJson = OneTaskListCalled(taskListId, "Errands");
        RegisterChatApiClient([]);
        var cut = RenderComponent<CalendarEventEditor>();

        cut.Find("#linkToTaskListSelect").Change(taskListId.ToString());
        ClickSave(cut);

        Assert.Equal($"/api/tasks/{taskListId}/items/calendar-event", _linkedToPath);
        Assert.Equal(SavedEventId, _linkedEventId);
    }

    [Fact]
    public void An_event_saved_without_choosing_a_list_is_put_on_none()
    {
        _taskListsJson = OneTaskListCalled(Guid.NewGuid(), "Errands");
        RegisterChatApiClient([]);
        var cut = RenderComponent<CalendarEventEditor>();

        ClickSave(cut);

        Assert.Null(_linkedToPath);
    }

    /// <summary>
    /// Reopening an event that is already on a list shows that list, rather than "No list" - which read
    /// as the link never having been saved, since nothing else on the page says it was.
    /// </summary>
    [Fact]
    public void An_event_already_on_a_list_opens_with_that_list_chosen()
    {
        var taskListId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        _taskListsJson = OneTaskListCalled(taskListId, "Errands", holdingAnEntryFor: eventId);
        _existingEvent = AnEventCalled(eventId, "Dentist");
        RegisterChatApiClient([]);

        var cut = RenderComponent<CalendarEventEditor>(parameters => parameters.Add(editor => editor.Id, eventId));

        Assert.Equal(taskListId.ToString(), cut.Find("#linkToTaskListSelect").GetAttribute("value"));
    }

    /// <summary>Save is an icon at the head of the page now - see EditorActions.razor.</summary>
    private static void ClickSave(IRenderedFragment cut)
        => cut.FindAll("button")
            .First(button => string.Equals(button.GetAttribute("aria-label"), "Save", StringComparison.Ordinal))
            .Click();

    private static CalendarEventDto AnEventCalled(Guid id, string title)
        => new(
            id,
            new CalendarEventDetailsDto(
                title, null, null, null, new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 1, 11, 0, 0, TimeSpan.Zero), IsAllDay: false, Recurrence: null,
                Guests: [], ReminderMinutesBeforeStart: [], ReminderNotificationChannel: "None"),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, IsShared: false, SharedByUserName: null,
            AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    /// <summary>An account with no lists is offered no picker, rather than an empty one.</summary>
    [Fact]
    public void With_no_lists_there_is_nothing_to_link_to()
    {
        RegisterChatApiClient([]);

        var cut = RenderComponent<CalendarEventEditor>();

        Assert.Empty(cut.FindAll("#linkToTaskListSelect"));
    }

    private void RegisterChatApiClient(IReadOnlyList<ContactDto> contacts)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(contacts))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new ChatApiClient(httpClient));
    }

    private static HttpResponseMessage JsonResponse(IReadOnlyList<ContactDto> contacts)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(contacts) };

    private static HttpResponseMessage JsonResponse<T>(T body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    /// <summary>
    /// The editor asks whether to offer the Google Calendar link. These tests are not about that link,
    /// so the account the gate sees qualifies for none - mirrors CalendarTests.
    /// </summary>
    private void RegisterGoogleIntegrationAccess()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new AccountDto(
                Guid.NewGuid(), "owner@example.com", "owner", "Owner",
                IsEmailVerified: false, HasPassword: true, IsGoogleLinked: false))
        });
        Services.AddSingleton(new GoogleIntegrationAccess(
            new UsersApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }),
            // Never initialised, so the extras are on - which leaves the account above as the only
            // thing deciding, and it is the thing these tests are pointed at.
            new DevicePreferences(new StubJSRuntime()),
            NullLogger<GoogleIntegrationAccess>.Instance));
    }

    /// <summary>
    /// Opened from the map: the place somebody pointed at is already in the box, with its pin, so they
    /// are asked only for what the map cannot know - what it is and when.
    /// </summary>
    [Fact]
    public void A_place_chosen_on_the_map_is_already_filled_in()
    {
        RegisterChatApiClient([]);
        Services.GetRequiredService<ChosenPlace>().Hold(new PickedPlace("Długa 4, Warszawa", 52.2497, 21.0122));

        var cut = RenderComponent<CalendarEventEditor>();

        Assert.Equal("Długa 4, Warszawa", LocationBoxOf(cut).GetAttribute("value"));
        // The pin, not only its name: without it the calendar has a label rather than a place. Asserted
        // through the control that only exists once one is set, rather than the printed coordinates,
        // which are formatted in whatever culture the page is running in.
        Assert.Contains("Remove location", cut.Markup);
    }

    /// <summary>
    /// Handed over once. Coming back to a new event later must start empty rather than at somewhere
    /// looked at yesterday - which is why ChosenPlace is taken rather than read.
    /// </summary>
    [Fact]
    public void The_chosen_place_is_only_used_once()
    {
        RegisterChatApiClient([]);
        var chosen = Services.GetRequiredService<ChosenPlace>();
        chosen.Hold(new PickedPlace("Długa 4, Warszawa", 52.2497, 21.0122));

        RenderComponent<CalendarEventEditor>();

        Assert.False(chosen.IsWaiting);
        Assert.Empty(LocationBoxOf(RenderComponent<CalendarEventEditor>()).GetAttribute("value")!);
    }

    private static AngleSharp.Dom.IElement LocationBoxOf(IRenderedComponent<CalendarEventEditor> cut)
        => cut.Find(".event-fields-location");

    [Fact]
    public void A_new_event_opened_without_the_map_has_no_place()
    {
        RegisterChatApiClient([]);

        Assert.Empty(LocationBoxOf(RenderComponent<CalendarEventEditor>()).GetAttribute("value")!);
    }
}
