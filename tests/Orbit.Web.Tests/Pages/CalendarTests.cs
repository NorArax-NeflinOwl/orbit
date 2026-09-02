using System.Net;
using System.Net.Http.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Tasks;
using Orbit.Contracts.Users;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

// The two tests that pressed "Show event list" and "Show task list" lived here. Both lists are one
// list now and it is always on, so there is nothing to reveal - see Calendar.razor. What they were
// really guarding, that the list is scoped to the period on screen, is still covered by the two
// tests further down that check the month and the year.
public sealed class CalendarTests : OrbitTestContext
{
    public CalendarTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterChatApiClient([]);
        RegisterTasksApiClient([]);
        // Only reached if an event carries a guest id missing from the (empty) contact list above - none
        // of these tests add guests, so this just needs to satisfy Calendar.razor's @inject.
        Services.AddSingleton(new UsersApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") }));
        RegisterGoogleIntegrationAccess();
        // Nothing stored, so both side panels start closed - which is what these tests assume, and what
        // a browser that has never had them opened gets.
        Services.AddSingleton(new PanelPreferences(new StubJSRuntime()));
        // Storage starts empty, so the list comes back in the order it has always been in - by when.
        //
        // Showing everything, deliberately: the list leaves out what is over, and most of these fixtures
        // sit on the fifteenth of the current month - which is in the past for half of every month. They
        // are about what the list draws and how it reads, not about what it hides, so they say so here
        // rather than each carrying a date chosen to dodge the rule. The two tests about the rule build
        // their own.
        Services.AddSingleton(ShowingEverything());
    }

    [Fact]
    public void The_calendar_opens_in_month_view_with_the_Month_button_marked_active()
    {
        RegisterCalendarApiClient([]);

        var cut = RenderComponent<Calendar>();

        Assert.NotEmpty(cut.FindAll(".calendar-month-grid"));
        Assert.Equal("true", FindViewSwitchButton(cut, "Month").GetAttribute("aria-pressed"));
        Assert.Equal("false", FindViewSwitchButton(cut, "Day").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Clicking_Day_switches_the_visualization_to_the_day_grid()
    {
        RegisterCalendarApiClient([]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Day").Click();

        Assert.NotEmpty(cut.FindAll(".calendar-day-grid"));
        Assert.Empty(cut.FindAll(".calendar-month-grid"));
        Assert.Equal("true", FindViewSwitchButton(cut, "Day").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Clicking_Year_switches_the_visualization_to_a_year_grid_with_all_12_months()
    {
        RegisterCalendarApiClient([]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Year").Click();

        Assert.Equal(12, cut.FindAll(".calendar-year-grid-month").Count);
    }



    [Fact]
    public void Todays_timed_event_shows_up_as_a_chip_named_but_not_timed_in_the_month_view()
    {
        var todayNoon = DateTime.SpecifyKind(DateTime.Today.AddHours(14).AddMinutes(30), DateTimeKind.Local);
        var calendarEvent = CreateTimedEvent(todayNoon, todayNoon.AddHours(1), "Team meeting");
        RegisterCalendarApiClient([calendarEvent]);

        var cut = RenderComponent<Calendar>();

        var chip = cut.Find(".calendar-event-chip");
        Assert.Contains("Team meeting", chip.TextContent);
        // No clock on a month cell: seven of these across a screen leaves a chip about as wide as
        // "00:00", so the time was spending the room the name needed. The day view reads times.
        Assert.DoesNotContain("14:30", chip.TextContent);
    }

    /// <summary>
    /// A long name is cut to its first two words. One word is often the least useful part of a name -
    /// "Ginekolog:" on its own says less than "Ginekolog: wizyta" - and the whole of it stays on the
    /// chip's title for anyone hovering.
    /// </summary>
    [Fact]
    public void A_long_name_is_shortened_to_two_words_on_a_month_cell()
    {
        var todayNoon = DateTime.SpecifyKind(DateTime.Today.AddHours(14).AddMinutes(30), DateTimeKind.Local);
        RegisterCalendarApiClient([CreateTimedEvent(todayNoon, todayNoon.AddHours(1), "Ginekolog: wizyta kontrolna")]);

        var cut = RenderComponent<Calendar>();

        var chip = cut.Find(".calendar-event-chip");
        Assert.Contains("Ginekolog: wizyta…", chip.TextContent);
        Assert.Equal("Ginekolog: wizyta kontrolna", chip.GetAttribute("title"));
    }

    [Fact]
    public void Todays_task_with_a_due_date_shows_up_as_a_task_chip_in_the_month_view()
    {
        var todayMorning = DateTime.SpecifyKind(DateTime.Today.AddHours(9), DateTimeKind.Local);
        var taskList = CreateTaskListWithDueItem(todayMorning, "Send the report");
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([taskList]);

        var cut = RenderComponent<Calendar>();

        var chip = cut.Find(".calendar-task-chip");
        Assert.Contains("Send the…", chip.TextContent);
        Assert.DoesNotContain("09:00", chip.TextContent);
    }

    [Fact]
    public void Todays_task_with_a_due_date_shows_up_on_the_day_grids_timeline_at_its_exact_due_time()
    {
        var todayMorning = DateTime.SpecifyKind(DateTime.Today.AddHours(9).AddMinutes(45), DateTimeKind.Local);
        var taskList = CreateTaskListWithDueItem(todayMorning, "Send the report");
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([taskList]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Day").Click();

        var block = cut.Find(".calendar-task-block");
        Assert.Contains("09:45", block.TextContent);
        Assert.Contains("Send the report", block.TextContent);
        // 9h45m since midnight is 585 of the day's 1440 minutes - see CalendarDayGrid's DueTaskPositionStyle.
        Assert.Contains("top:40.625", block.GetAttribute("style"));
    }

    [Fact]
    public void Year_view_shows_task_dots_by_default_and_hides_them_once_the_checkbox_is_unchecked()
    {
        var todayMorning = DateTime.SpecifyKind(DateTime.Today.AddHours(9), DateTimeKind.Local);
        var taskList = CreateTaskListWithDueItem(todayMorning, "Send the report");
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([taskList]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Year").Click();
        Assert.NotEmpty(cut.FindAll(".calendar-month-grid-day-dot-task"));

        cut.Find("#showDueTasksInYearView").Change(false);

        Assert.Empty(cut.FindAll(".calendar-month-grid-day-dot-task"));
    }

    private static IElement FindViewSwitchButton(IRenderedComponent<Calendar> cut, string label)
        => cut.Find(".calendar-view-switch").QuerySelectorAll("button").Single(button => button.TextContent == label);

    private static IElement FindButtonByTitle(IRenderedComponent<Calendar> cut, string title)
        => cut.FindAll("button").Single(button => button.GetAttribute("title") == title);

    private static CalendarEventDto CreateTimedEvent(DateTime localStart, DateTime localEnd, string title)
        => new(
            Guid.NewGuid(),
            new CalendarEventDetailsDto(
                title, null, null, null,
                new DateTimeOffset(DateTime.SpecifyKind(localStart, DateTimeKind.Local)),
                new DateTimeOffset(DateTime.SpecifyKind(localEnd, DateTimeKind.Local)),
                IsAllDay: false, null, [], [], "None", "None"),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, IsShared: false, SharedByUserName: null, AccessLevel: "ReadOnly", OriginalOwnerUserId: null);

    private static TaskDto CreateTaskListWithDueItem(DateTime localDueDate, string description)
    {
        var item = new TaskItemDto(
            Guid.NewGuid(), description, new DateTimeOffset(DateTime.SpecifyKind(localDueDate, DateTimeKind.Local)), IsCompleted: false,
            LinkedTaskListId: null, OverdueNotificationChannel: "None", RemindDaily: false, DailyReminderNotificationChannel: "None",
            DailyReminderTimeOfDay: default);
        return new TaskDto(
            Guid.NewGuid(), "Task list", [item], IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "ReadOnly", OriginalOwnerUserId: null);
    }


    /// <summary>
    /// The pages inject this to decide whether to offer the Google links. Registered over a stubbed
    /// account rather than a live one: a real HttpClient here would spend wall-clock time on a DNS
    /// lookup bUnit's synchronous render doesn't wait out.
    /// </summary>
    private void RegisterGoogleIntegrationAccess()
    {
        // These tests are not about the Google links, so the account the gate sees qualifies for none.
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

    private void RegisterChatApiClient(IReadOnlyList<ContactDto> contacts)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(contacts))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new ChatApiClient(httpClient));
    }

    private void RegisterCalendarApiClient(IReadOnlyList<CalendarEventDto> events)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(events))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new CalendarApiClient(httpClient));
    }

    private void RegisterTasksApiClient(IReadOnlyList<TaskDto> taskLists)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(taskLists))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new TasksApiClient(httpClient));
    }

    private static HttpResponseMessage JsonResponse<TItem>(IReadOnlyList<TItem> items)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(items) };
    [Fact]
    public void The_event_list_covers_the_month_on_screen_and_not_the_whole_calendar()
    {
        // Listing everything meant scrolling past last spring to find next week.
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClient([
            CreateTimedEvent(midMonth, midMonth.AddHours(1), "This month"),
            CreateTimedEvent(midMonth.AddMonths(2), midMonth.AddMonths(2).AddHours(1), "Two months on")]);
        RegisterTasksApiClient([]);
        var cut = RenderComponent<Calendar>();

        Assert.Contains("This month", cut.Markup);
        Assert.DoesNotContain("Two months on", cut.Markup);
    }

    /// <summary>
    /// What a calendar is read for is what is coming. By the twentieth of a month, a month's worth of
    /// finished work is what a reader has to scroll past to find it - so the list leaves out what is
    /// over, and the menu is how it is asked for.
    /// </summary>
    [Fact]
    public void A_ticked_off_deadline_is_not_listed_until_everything_is_asked_for()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([
            CreateTaskListWithDueItem(midMonth, "Still to do"),
            TickedOff(CreateTaskListWithDueItem(midMonth, "Already done"))]);

        var cut = RenderComponent<Calendar>();

        Assert.Equal(["Still to do"], ListedNames(cut));

        cut.Find(".page-header-actions .overflow-menu-trigger").Click();
        cut.FindAll(".page-header-actions .avatar-dropdown-item")
            .First(entry => entry.TextContent.Contains("Everything", StringComparison.Ordinal))
            .Click();

        Assert.Equal(["Still to do", "Already done"], ListedNames(cut));
    }

    /// <summary>
    /// An event that has already ended is over the same way. A deadline that has passed and is still not
    /// ticked off is not: it is the one thing on the page that most needs saying.
    /// </summary>
    [Fact]
    public void An_event_that_has_ended_goes_but_an_overdue_deadline_stays()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        // Midnight this morning: always in the month being listed, and always already past. A time
        // relative to "now" would leave the month on the first of it and the test would then be asking
        // about a period the list is not showing.
        var earlierToday = DateTime.Today;
        RegisterCalendarApiClient([CreateTimedEvent(earlierToday, earlierToday, "Over and done with")]);
        RegisterTasksApiClient([CreateTaskListWithDueItem(earlierToday, "Still not done")]);

        var cut = RenderComponent<Calendar>();

        Assert.Equal(["Still not done"], ListedNames(cut));
    }

    /// <summary>
    /// An entry tied to an event is that event. Listing both put the same appointment on the page twice,
    /// one card under the other - the grids have always dropped one, and the list beside them had not.
    /// </summary>
    [Fact]
    public void An_entry_that_is_an_event_is_listed_once()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        var calendarEvent = CreateTimedEvent(midMonth, midMonth.AddHours(1), "Dentist");
        RegisterCalendarApiClient([calendarEvent]);
        RegisterTasksApiClient([TaskListWithAnEntryFor(calendarEvent.Id, midMonth, "Dentist")]);

        var cut = RenderComponent<Calendar>();

        Assert.Equal(["Dentist"], ListedNames(cut));
    }

    /// <summary>
    /// And the card that survives says which list it came from. The entry carried that and the event did
    /// not, so folding the two into one card would otherwise have lost it.
    /// </summary>
    [Fact]
    public void The_one_card_says_which_list_the_appointment_is_on()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        var calendarEvent = CreateTimedEvent(midMonth, midMonth.AddHours(1), "Dentist");
        RegisterCalendarApiClient([calendarEvent]);
        RegisterTasksApiClient([TaskListWithAnEntryFor(calendarEvent.Id, midMonth, "Dentist")]);

        var cut = RenderComponent<Calendar>();

        Assert.Contains("Errands", cut.Find(".item-card-meta").TextContent);
    }

    /// <summary>
    /// An appointment a list made opens as that entry rather than as the event: the list is where it is
    /// worked from, and the full event form is a different thing from looking at what is coming. Which
    /// is the entry's own summary, because an appointment has a place - see GoToDueTask and HasPlace.
    /// </summary>
    [Fact]
    public void An_appointment_a_list_made_opens_as_the_entry_behind_it()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        var calendarEvent = CreateTimedEvent(midMonth, midMonth.AddHours(1), "Dentist");
        var taskList = TaskListWithAnEntryFor(calendarEvent.Id, midMonth, "Dentist");
        RegisterCalendarApiClient([calendarEvent]);
        RegisterTasksApiClient([taskList]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Calendar>();

        cut.Find(".item-card-name").Click();

        Assert.EndsWith($"/tasks/{taskList.Id}/items/{taskList.Items[0].Id}", navigationManager.Uri);
        Assert.DoesNotContain("/calendar/", navigationManager.Uri);
    }

    /// <summary>An event nothing made still opens as itself.</summary>
    [Fact]
    public void An_event_of_its_own_still_opens_as_the_event()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        var calendarEvent = CreateTimedEvent(midMonth, midMonth.AddHours(1), "Dentist");
        RegisterCalendarApiClient([calendarEvent]);
        RegisterTasksApiClient([]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Calendar>();

        cut.Find(".item-card-name").Click();

        Assert.EndsWith($"/calendar/{calendarEvent.Id}", navigationManager.Uri);
    }

    /// <summary>
    /// An appointment on a list carries no due date of its own - the event says when it is - so it is
    /// not among the due tasks at all, and the link has to be read off the lists themselves.
    /// </summary>
    private static TaskDto TaskListWithAnEntryFor(Guid calendarEventId, DateTime when, string description)
        => new(
            Guid.NewGuid(), "Errands",
            [new TaskItemDto(
                Guid.NewGuid(), description, DueDateUtc: null, IsCompleted: false, LinkedTaskListId: null,
                OverdueNotificationChannel: "None", RemindDaily: false, DailyReminderNotificationChannel: "Push",
                DailyReminderTimeOfDay: default, Kind: "Calendar", Location: "",
                LinkedCalendarEventId: calendarEventId)],
            IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, IsShared: false, SharedByUserName: null,
            AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    /// <summary>
    /// The list's default, and the reason it exists: what is coming, soonest first. The two orders
    /// below are for a reader looking for one thing rather than reading the period.
    /// </summary>
    [Fact]
    public void The_list_comes_in_the_order_things_happen()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClient([
            CreateTimedEvent(midMonth.AddDays(2), midMonth.AddDays(2).AddHours(1), "Beta"),
            CreateTimedEvent(midMonth, midMonth.AddHours(1), "Zulu")]);
        RegisterTasksApiClient([]);

        var cut = RenderComponent<Calendar>();

        Assert.Equal(["Zulu", "Beta"], ListedNames(cut));
    }

    [Fact]
    public void Sorting_by_type_puts_the_events_before_the_deadlines()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        // The deadline is first by when, so only the order asked for can put the event in front of it.
        RegisterCalendarApiClient([CreateTimedEvent(midMonth.AddDays(1), midMonth.AddDays(1).AddHours(1), "An event")]);
        RegisterTasksApiClient([CreateTaskListWithDueItem(midMonth, "A deadline")]);
        var cut = RenderComponent<Calendar>();

        SortBy(cut, "By type");

        Assert.Equal(["An event", "A deadline"], ListedNames(cut));
    }

    [Fact]
    public void Sorting_alphabetically_orders_by_name_whatever_kind_of_thing_it_is()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClient([CreateTimedEvent(midMonth, midMonth.AddHours(1), "Zulu")]);
        RegisterTasksApiClient([CreateTaskListWithDueItem(midMonth.AddDays(1), "Alpha")]);
        var cut = RenderComponent<Calendar>();

        SortBy(cut, "Alphabetical");

        Assert.Equal(["Alpha", "Zulu"], ListedNames(cut));
    }

    private static void SortBy(IRenderedFragment cut, string label)
    {
        cut.Find(".calendar-event-list-panel .overflow-menu-trigger, .page-header-actions .overflow-menu-trigger").Click();
        cut.FindAll(".avatar-dropdown-item").First(item => item.TextContent.Contains(label)).Click();
    }

    /// <summary>The same list with its one entry crossed off.</summary>
    private static TaskDto TickedOff(TaskDto taskList)
        => taskList with { Items = [taskList.Items[0] with { IsCompleted = true }] };

    /// <summary>A list order that hides nothing - see the constructor for why most of these want one.</summary>
    private static CalendarListOrder ShowingEverything()
    {
        var listOrder = new CalendarListOrder(new StubJSRuntime());
        listOrder.ShowEverythingAsync(true).GetAwaiter().GetResult();
        return listOrder;
    }

    private static string[] ListedNames(IRenderedFragment cut)
        => [.. cut.FindAll(".item-card-name-text").Select(name => name.TextContent.Trim())];

    [Fact]
    public void The_year_view_lists_the_year_rather_than_the_month()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        var laterThisYear = new DateTime(DateTime.Today.Year, 12, 20, 10, 0, 0);
        var nextYear = new DateTime(DateTime.Today.Year + 1, 3, 1, 10, 0, 0);
        RegisterCalendarApiClient([
            CreateTimedEvent(midMonth, midMonth.AddHours(1), "This month"),
            CreateTimedEvent(laterThisYear, laterThisYear.AddHours(1), "December"),
            CreateTimedEvent(nextYear, nextYear.AddHours(1), "Next year")]);
        RegisterTasksApiClient([]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Year").Click();

        Assert.Contains("This month", cut.Markup);
        Assert.Contains("December", cut.Markup);
        Assert.DoesNotContain("Next year", cut.Markup);
    }
}
