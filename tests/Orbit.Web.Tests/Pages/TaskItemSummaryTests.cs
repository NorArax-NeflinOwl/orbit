using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        // Edit is not among them: it is a button of its own on the panel now - see EditorRail.OnEdit.
        Assert.DoesNotContain("Edit", offered);
        Assert.Single(cut.FindAll(".editor-rail button[aria-label=Edit]"));
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
        cut.FindAll(".editor-rail button").First(button => button.GetAttribute("aria-label") == "Edit").Click();

        Assert.EndsWith($"/tasks/{TaskListId}/items/{ItemId}/edit", navigationManager.Uri);
    }

    /// <summary>
    /// And it carries where the reader came from with it, so the edit ends back there. This page is the
    /// middle hop of the case that was reported: an appointment pressed on the calendar opens as its
    /// entry here, and saving from the form beyond used to land on /tasks. See ReturnTo.
    /// </summary>
    [Fact]
    public void Edit_carries_the_page_the_reader_came_from()
    {
        RegisterClients(Item("Dentist", DateTimeOffset.UtcNow.AddDays(1), location: "Przychodnia"));
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo(navigationManager.GetUriWithQueryParameter(ReturnTo.QueryName, "/calendar"));
        var cut = Render();

        cut.Find(".editor-rail .overflow-menu-trigger").Click();
        cut.FindAll(".editor-rail button").First(button => button.GetAttribute("aria-label") == "Edit").Click();

        Assert.EndsWith(
            $"/tasks/{TaskListId}/items/{ItemId}/edit?{ReturnTo.QueryName}=%2Fcalendar", navigationManager.Uri);
    }

    /// <summary>The one press that leaves without opening a menu goes back to the list it is on.</summary>
    [Fact]
    public void Cancel_goes_back_to_the_list()
    {
        RegisterClients(Item("Dentist", DateTimeOffset.UtcNow.AddDays(1), location: "Przychodnia"));
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = Render();

        cut.FindAll("button").First(button => button.GetAttribute("aria-label") == "Back").Click();

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

    /// <summary>
    /// The day and the hour of a calendar entry live on its appointment, not on the entry - the editor
    /// writes them there. This page read only the entry, so it said "no date set" about an appointment
    /// that plainly had one, on the very page somebody opens to find out when to set off.
    /// </summary>
    [Fact]
    public void An_appointment_says_when_it_happens_rather_than_that_it_has_no_date()
    {
        var eventId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);
        var calendarEvent = CalendarEvent(eventId, "Rynek Główny 1") with
        {
            Details = CalendarEvent(eventId, "Rynek Główny 1").Details with
            {
                StartUtc = start,
                EndUtc = start.AddHours(1)
            }
        };
        // The entry carries no due date of its own, which is what an appointment made in the task
        // editor actually looks like.
        RegisterClients(Item("Dentist", dueDateUtc: null, "", eventId), calendarEvent);

        var cut = Render();

        Assert.DoesNotContain("No date set", cut.Markup);
        Assert.Contains(start.LocalDateTime.ToString("dd.MM.yyyy"), cut.Markup);
        Assert.Contains(start.LocalDateTime.ToString("HH:mm"), cut.Markup);
    }

    /// <summary>A deadline with no appointment behind it still shows its own date.</summary>
    [Fact]
    public void A_deadline_of_its_own_still_says_when_it_is_due()
    {
        var due = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);
        RegisterClients(Item("Pay the rent", due, ""));

        var cut = Render();

        Assert.Contains(due.LocalDateTime.ToString("dd.MM.yyyy HH:mm"), cut.Markup);
    }

    [Fact]
    public void An_entry_with_no_date_anywhere_says_so()
    {
        RegisterClients(Item("Pay the rent", dueDateUtc: null, ""));

        var cut = Render();

        Assert.Contains("No date set", cut.Markup);
    }

    /// <summary>
    /// Pressing an appointment on the calendar opens it as the entry that raised it, while the reminder
    /// for it points at the event - so nothing in this page's own address settles that notification and
    /// the bell stayed lit over something the reader was looking at. See NewsSettler.
    /// </summary>
    [Fact]
    public void Opening_the_entry_settles_the_notification_about_its_appointment()
    {
        var eventId = Guid.NewGuid();
        // Registered before anything is resolved: bUnit freezes the container the moment a service is
        // read out of it.
        RegisterClients(Item("Dentist", dueDateUtc: null, "", eventId), CalendarEvent(eventId, "Rynek Główny 1"));
        var feed = Services.GetRequiredService<NotificationFeedState>();
        feed.Set([new Orbit.Contracts.Notifications.NotificationEntryDto(
            Guid.NewGuid(), "EventReminder", "Coming up", "Dentist at 10:00.", $"/calendar/{eventId}",
            DateTimeOffset.UtcNow, IsRead: false)]);

        Render();

        Assert.Equal(0, feed.UnreadCount);
        Assert.Contains(_markedReadAt, url => url == $"/calendar/{eventId}");
    }

    /// <summary>An entry that stands for no appointment settles nothing that is not its own.</summary>
    [Fact]
    public void An_entry_with_no_appointment_settles_nothing_of_its_own()
    {
        RegisterClients(Item("Pay the rent", DateTimeOffset.UtcNow, ""));
        var feed = Services.GetRequiredService<NotificationFeedState>();
        feed.Set([new Orbit.Contracts.Notifications.NotificationEntryDto(
            Guid.NewGuid(), "EventReminder", "Coming up", "Something else.", $"/calendar/{Guid.NewGuid()}",
            DateTimeOffset.UtcNow, IsRead: false)]);

        Render();

        Assert.Equal(1, feed.UnreadCount);
        Assert.Empty(_markedReadAt);
    }

    /// <summary>
    /// The entry's own screen crosses it off. It used to say "Already done." and nothing else, so the
    /// one screen about this entry was the one place the entry could not be finished - a reader who
    /// opened it to check when something was due had to go back to the list to tick it.
    ///
    /// Written the moment the box is filled in, the way the checklist writes it: the endpoint replaces
    /// the list wholesale, so the whole list goes back with this one entry changed.
    /// </summary>
    [Fact]
    public void The_entry_is_crossed_off_where_it_is_read()
    {
        RegisterClients(Item("Pay the rent", DateTimeOffset.UtcNow, ""));
        var cut = Render();

        cut.Find(".check-row .tick-box").Click();

        var saved = JsonDocument.Parse(Assert.Single(_savedLists)).RootElement;
        Assert.Equal("Errands", saved.GetProperty("title").GetString());
        Assert.True(saved.GetProperty("items")[0].GetProperty("isCompleted").GetBoolean());
        // And the box says so afterwards, because the page re-read the list it had just written.
        Assert.Contains("tick-box-done", cut.Find(".check-row .tick-box").ClassList);
    }

    /// <summary>
    /// The box gives three answers, one press at a time: done, given up on, and nothing again - see
    /// TickState. Taking a tick back is the third press, which is the price of the cross being reachable
    /// without a menu.
    /// </summary>
    [Fact]
    public void The_box_goes_round_done_then_given_up_on_then_back()
    {
        RegisterClients(Item("Pay the rent", DateTimeOffset.UtcNow, "") with { IsCompleted = true });
        var cut = Render();

        cut.Find(".check-row .tick-box").Click();

        var crossedOut = JsonDocument.Parse(_savedLists[^1]).RootElement.GetProperty("items")[0];
        Assert.False(crossedOut.GetProperty("isCompleted").GetBoolean());
        Assert.True(crossedOut.GetProperty("isFailed").GetBoolean());
        Assert.Contains("tick-box-failed", cut.Find(".check-row .tick-box").ClassList);

        cut.Find(".check-row .tick-box").Click();

        var cleared = JsonDocument.Parse(_savedLists[^1]).RootElement.GetProperty("items")[0];
        Assert.False(cleared.GetProperty("isCompleted").GetBoolean());
        Assert.False(cleared.GetProperty("isFailed").GetBoolean());
        Assert.DoesNotContain("tick-box-done", cut.Find(".check-row .tick-box").ClassList);
        Assert.DoesNotContain("tick-box-failed", cut.Find(".check-row .tick-box").ClassList);
    }

    /// <summary>
    /// A list handed over to be read says what it says and offers nothing. The server refuses the save
    /// anyway; a box that answers a press with a refusal is worse than one that never offered.
    /// </summary>
    [Fact]
    public void A_list_shared_to_read_shows_the_tick_without_offering_it()
    {
        RegisterClients(Item("Pay the rent", DateTimeOffset.UtcNow, ""), accessLevel: "ReadOnly");

        var cut = Render();

        Assert.True(cut.Find(".check-row .tick-box").HasAttribute("disabled"));
    }

    /// <summary>
    /// An entry standing for other lists is done when they are (see LinkedTaskCompletionResolver), so
    /// there is no box to fill in here at all - the row names the list the answer is on and offers to
    /// go there, which is what the checklist answers a press on such a box with.
    /// </summary>
    [Fact]
    public void An_entry_that_stands_for_another_list_says_where_the_tick_belongs()
    {
        var kitchen = new TaskDto(
            Guid.NewGuid(), "Kitchen", [], IsCompleted: false, IsGroup: false, IsPrivate: false,
            EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, IsShared: false,
            SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);
        RegisterClients(
            Item("Kitchen done", null, "") with { LinkedTaskListId = kitchen.Id }, otherLists: [kitchen]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = Render();

        Assert.Empty(cut.FindAll(".check-row .tick-box"));
        Assert.Contains("This is done when Kitchen is.", cut.Markup);

        cut.FindAll("button").First(button => button.TextContent.Trim() == "Kitchen").Click();

        Assert.EndsWith($"/tasks/{kitchen.Id}", navigationManager.Uri);
    }

    /// <summary>
    /// A tick the server would not take is said on the page rather than swallowed: somebody else is in
    /// the list's form, and a box that snaps back with no word for it reads as a press that never
    /// registered.
    /// </summary>
    [Fact]
    public void A_tick_the_server_refuses_is_said_on_the_page()
    {
        RegisterClients(Item("Pay the rent", DateTimeOffset.UtcNow, ""));
        _answerToATick = () => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new { LockedByUserName = "anna" })
        };
        var cut = Render();

        cut.Find(".check-row .tick-box").Click();

        Assert.Contains("anna is currently editing", cut.Find(".error").TextContent);
        // And the box is back to what the server holds rather than left standing ticked.
        Assert.DoesNotContain("tick-box-done", cut.Find(".check-row .tick-box").ClassList);
    }

    /// <summary>Every address this page asked the server to mark read.</summary>
    private readonly List<string> _markedReadAt = [];

    private static TaskItemDto Item(
        string description, DateTimeOffset? dueDateUtc, string location, Guid? linkedCalendarEventId = null)
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

    /// <summary>What every tick sent, in the order it was sent - see The_entry_is_crossed_off_here.</summary>
    private readonly List<string> _savedLists = [];

    /// <summary>
    /// What the server answers a tick with. NoContent unless a test says otherwise, which is what a
    /// save that went through looks like.
    /// </summary>
    private Func<HttpResponseMessage> _answerToATick =
        () => new HttpResponseMessage(HttpStatusCode.NoContent);

    /// <param name="accessLevel">
    /// What this reader may do with the list the entry is on - "ReadOnly" for one shared to be read.
    /// </param>
    /// <param name="otherLists">
    /// The other lists this account has, which is what an entry standing for one is named from - see
    /// the page's own NameTheListsBehindTheEntryAsync.
    /// </param>
    private void RegisterClients(
        TaskItemDto? taskItem, CalendarEventDto? calendarEvent = null, string accessLevel = "CanEdit",
        IReadOnlyList<TaskDto>? otherLists = null)
    {
        var taskList = new TaskDto(
            TaskListId, "Errands", taskItem is null ? [] : [taskItem], IsCompleted: false, IsGroup: false,
            IsPrivate: false, EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: accessLevel, OriginalOwnerUserId: null);
        // What the server holds, which a tick changes: a page that re-reads the list after saving
        // should see what it just wrote, the way it would against the real thing.
        var stored = taskList;
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Put)
            {
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                _savedLists.Add(body);
                var answer = _answerToATick();
                if (answer.IsSuccessStatusCode)
                {
                    stored = Ticked(stored, body);
                }

                return answer;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = request.RequestUri!.AbsolutePath.Contains("/calendar", StringComparison.Ordinal)
                    ? JsonContent.Create(calendarEvent)
                    : request.RequestUri!.AbsolutePath.EndsWith("/chat/contacts", StringComparison.Ordinal)
                        ? JsonContent.Create(_contacts)
                        // "api/tasks" is every list this account has; "api/tasks/{id}" is the one the
                        // entry is on. The first is what names a list this entry stands for.
                        : request.RequestUri!.AbsolutePath.TrimEnd('/').EndsWith("/api/tasks", StringComparison.Ordinal)
                            ? JsonContent.Create<IReadOnlyList<TaskDto>>([stored, .. otherLists ?? []])
                            : JsonContent.Create(stored)
            };
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new CalendarApiClient(httpClient));
        // Over the base context's own, so a test can say which address this page settled - see
        // NewsSettler, and Opening_the_entry_settles_the_notification_about_its_appointment.
        var notifications = new NotificationsApiClient(new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/notifications/read-at", StringComparison.Ordinal))
            {
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                _markedReadAt.Add(
                    JsonDocument.Parse(body).RootElement.GetProperty("url").GetString() ?? string.Empty);
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        });
        Services.AddSingleton(notifications);
        Services.AddScoped(services => new NewsSettler(notifications, services.GetRequiredService<NotificationFeedState>()));
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

    /// <summary>
    /// The list as a save left it: what was sent back is what the server would now hold. Only the two
    /// completion flags are taken from the request, which is all a tick changes.
    /// </summary>
    private static TaskDto Ticked(TaskDto taskList, string saved)
    {
        var items = JsonDocument.Parse(saved).RootElement.GetProperty("items");
        return taskList with
        {
            Items =
            [
                .. taskList.Items.Select((item, index) => item with
                {
                    IsCompleted = items[index].GetProperty("isCompleted").GetBoolean(),
                    IsFailed = items[index].GetProperty("isFailed").GetBoolean()
                })
            ]
        };
    }
}
