using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Tasks;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// One appointment on its own page: what it is, when, and where. This is what a deadline with a place
/// opens as from the calendar - the checklist is the right landing for something to tick off and the
/// wrong one for something you have to get to.
/// </summary>
public sealed class TaskItemSummaryTests : OrbitTestContext
{
    private static readonly Guid TaskListId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    /// <summary>Who this account knows, for naming the guests on an appointment.</summary>
    private readonly List<ContactDto> _contacts = [];

    public TaskItemSummaryTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        // No map is drawn in these tests: what they are about is what the page says, and Leaflet is not
        // loaded here. The stub answers the module import with a do-nothing module.
        JSInterop.SetupModule("./js/locationMap.js").SetupVoid("showLocation", _ => true);
    }

    private IRenderedComponent<TaskItemSummary> Render()
        => RenderComponent<TaskItemSummary>(parameters => parameters
            .Add(page => page.TaskListId, TaskListId)
            .Add(page => page.ItemId, ItemId));

    [Fact]
    public void The_page_says_what_it_is_when_it_is_and_where()
    {
        RegisterClients(Item("Dentist", DateTimeOffset.UtcNow.AddDays(1), location: "Przychodnia, Długa 4"));

        var cut = Render();

        Assert.Contains("Dentist", cut.Find("h1").TextContent);
        Assert.Contains("Errands", cut.Find(".page-subtitle").TextContent);
        Assert.Contains("Przychodnia, Długa 4", cut.Markup);
    }

    /// <summary>
    /// Everywhere this entry leads, in the panel every other screen keeps its actions in - and the
    /// form, which this page offered no way to reach at all.
    /// </summary>
    [Fact]
    public void Every_way_out_is_offered_in_the_panel()
    {
        RegisterClients(Item("Dentist", DateTimeOffset.UtcNow.AddDays(1), location: "Przychodnia"));

        var cut = Render();
        cut.Find(".editor-rail .overflow-menu-trigger").Click();

        var offered = cut.FindAll(".editor-rail .avatar-dropdown-item").Select(entry => entry.TextContent.Trim()).ToList();
        Assert.Contains("Back to Calendar", offered);
        Assert.Contains("Show Tasks", offered);
        Assert.Contains("Edit", offered);
        // Nothing on this screen can be written, so the panel carries no Save.
        Assert.Empty(cut.FindAll(".editor-rail .page-action-primary"));
    }

    [Fact]
    public void Show_Tasks_opens_the_list_this_entry_is_on()
    {
        RegisterClients(Item("Dentist", DateTimeOffset.UtcNow.AddDays(1), location: "Przychodnia"));
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = Render();

        cut.Find(".editor-rail .overflow-menu-trigger").Click();
        cut.FindAll(".editor-rail .avatar-dropdown-item").First(entry => entry.TextContent.Contains("Show Tasks")).Click();

        // The shallow level, like every other way into a list.
        Assert.EndsWith($"/tasks/{TaskListId}", navigationManager.Uri);
    }

    /// <summary>
    /// Edit leads to the entry's own address rather than the list's form for its own sake - see
    /// TaskEditor's "/tasks/{listId}/items/{itemId}/edit" route, which opens on this one entry already
    /// unfolded.
    /// </summary>
    [Fact]
    public void Edit_opens_the_entrys_own_form()
    {
        RegisterClients(Item("Dentist", DateTimeOffset.UtcNow.AddDays(1), location: "Przychodnia"));
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = Render();

        cut.Find(".editor-rail .overflow-menu-trigger").Click();
        cut.FindAll(".editor-rail .avatar-dropdown-item").First(entry => entry.TextContent.Contains("Edit")).Click();

        Assert.EndsWith($"/tasks/{TaskListId}/items/{ItemId}/edit", navigationManager.Uri);
    }

    /// <summary>The one press that leaves without opening a menu goes back to the list it is on.</summary>
    [Fact]
    public void Cancel_goes_back_to_the_list()
    {
        RegisterClients(Item("Dentist", DateTimeOffset.UtcNow.AddDays(1), location: "Przychodnia"));
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = Render();

        cut.FindAll("button").First(button => button.GetAttribute("aria-label") == "Cancel").Click();

        Assert.EndsWith($"/tasks/{TaskListId}", navigationManager.Uri);
    }

    [Fact]
    public void An_entry_tied_to_an_event_takes_the_place_from_the_event()
    {
        // The event is the one place the address is stored, which is the whole point of the link.
        var eventId = Guid.NewGuid();
        RegisterClients(
            Item("Dentist", DateTimeOffset.UtcNow.AddDays(1), location: "", linkedCalendarEventId: eventId),
            CalendarEvent(eventId, "Przychodnia, Długa 4"));

        var cut = Render();

        Assert.Contains("Przychodnia, Długa 4", cut.Markup);
    }

    [Fact]
    public void An_entry_that_is_gone_says_so_rather_than_showing_an_empty_page()
    {
        RegisterClients(taskItem: null);

        var cut = Render();

        Assert.Contains("no longer exists", cut.Markup);
    }

    /// <summary>
    /// What the appointment says about itself and who is coming, above the map: both live on the event
    /// rather than on the entry, and the entry's own page is where somebody looks before setting off.
    /// </summary>
    [Fact]
    public void An_appointment_says_what_it_is_about_and_who_is_coming()
    {
        var guestId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        _contacts.Add(new ContactDto(
            guestId, "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false));
        var calendarEvent = CalendarEvent(eventId, "Rynek Główny 1") with
        {
            Details = CalendarEvent(eventId, "Rynek Główny 1").Details with
            {
                Description = "Bring the x-rays",
                Guests = [guestId]
            }
        };
        RegisterClients(Item("Dentist", DateTimeOffset.UtcNow, "", eventId), calendarEvent);

        var cut = Render();

        Assert.Contains("Bring the x-rays", cut.Markup);
        Assert.Contains("anna", cut.Markup);
    }

    private static TaskItemDto Item(
        string description, DateTimeOffset dueDateUtc, string location, Guid? linkedCalendarEventId = null)
        => new(
            ItemId, description, dueDateUtc, IsCompleted: false, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: new TimeOnly(9, 0),
            Kind: "Calendar", Location: location, LinkedCalendarEventId: linkedCalendarEventId);

    private static CalendarEventDto CalendarEvent(Guid id, string address)
        => new(
            id,
            new CalendarEventDetailsDto(
                "Dentist", null, new EventLocationDto(address, 52.23, 21.01), null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
                IsAllDay: false, null, [], [], "None", "None"),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private void RegisterClients(TaskItemDto? taskItem, CalendarEventDto? calendarEvent = null)
    {
        var taskList = new TaskDto(
            TaskListId, "Errands", taskItem is null ? [] : [taskItem], IsCompleted: false, IsGroup: false,
            IsPrivate: false, EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = request.RequestUri!.AbsolutePath.Contains("/calendar", StringComparison.Ordinal)
                    ? JsonContent.Create(calendarEvent)
                    : request.RequestUri!.AbsolutePath.EndsWith("/chat/contacts", StringComparison.Ordinal)
                        ? JsonContent.Create(_contacts)
                        : JsonContent.Create(taskList)
            }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new CalendarApiClient(httpClient));
        // Who is coming, when an appointment has guests. The same transport: it answers contacts with
        // the list below and anything else with the task list, which no assertion here reads.
        Services.AddSingleton(new ChatApiClient(httpClient));
        Services.AddSingleton(new UsersApiClient(httpClient));
        // Nominatim is a third party and is never called in a test: an address that resolves to nothing
        // is exactly the "words but no pin" case, which is what these assertions are about.
        Services.AddSingleton(new GeocodingApiClient(
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<object>())
            }))
            { BaseAddress = new Uri("https://geocode.test/") }));
    }
}
