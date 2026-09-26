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
using Orbit.Core.Tasks;
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

    /// <summary>
    /// Choosing in a period with nothing in it still offers the way back out. The bar was drawn inside
    /// the list, which such a period does not draw, so Select took its own button away and left nothing.
    /// </summary>
    [Fact]
    public void Selecting_in_an_empty_period_still_offers_the_way_out()
    {
        RegisterCalendarApiClient([]);
        var cut = RenderComponent<Calendar>();

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Select").Click();

        cut.FindAll(".picked-bar button").Single(button => button.TextContent.Contains("Stop selecting")).Click();
        Assert.Contains(cut.FindAll("button"), button => button.TextContent.Trim() == "Select");
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

    /// <summary>
    /// A week inside one month names the month once, at the end - "7 - 13 September 2026". The start
    /// used to come out as "9/7/2026", because a single-letter format string is read as a *standard*
    /// specifier and "d" standing alone is the short-date pattern rather than the day number.
    /// </summary>
    [Fact]
    public void A_week_within_one_month_is_headed_with_two_day_numbers_and_one_month()
    {
        RegisterCalendarApiClient([]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Week").Click();

        var label = cut.Find(".calendar-period-label").TextContent.Trim();
        Assert.DoesNotContain("/", label, StringComparison.Ordinal);
        // Two ends and one month name, whichever week today happens to fall in - a week straddling a
        // month says both names and still carries no short date.
        Assert.Contains(" - ", label, StringComparison.Ordinal);
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

    /// <summary>
    /// Opening an appointment says where it was opened from - the view being read and the day it was
    /// built around - so leaving it comes back to that rather than to the calendar in general.
    ///
    /// Every way out of this page carried plain "/calendar", and a page opened that way starts on today
    /// in the month: somebody who stepped forward, opened an appointment and came back landed in
    /// whatever month it is now, with what they were reading nowhere on screen (reported 2026-09-20).
    /// </summary>
    [Fact]
    public void Opening_an_event_says_which_day_it_was_opened_from()
    {
        var nextMonthNoon = DateTime.SpecifyKind(
            DateTime.Today.AddMonths(1).AddHours(12), DateTimeKind.Local);
        RegisterCalendarApiClient([CreateTimedEvent(nextMonthNoon, nextMonthNoon.AddHours(1), "Dentist")]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<Calendar>();
        // Forward a month, so "where the reader is" and "today" are two different days. The card in the
        // list beside the grid is what opens an appointment - a chip on a month cell is a label, and
        // the cell itself opens the day.
        cut.Find(".calendar-visualization-toolbar-navigation").QuerySelectorAll("button")
            .Single(button => button.TextContent.Trim() == "›")
            .Click();
        cut.Find(".item-card-name").Click();

        var landedOn = Uri.UnescapeDataString(navigationManager.Uri);
        Assert.Contains("returnTo=", landedOn, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            DateOnly.FromDateTime(DateTime.Today.AddMonths(1)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            landedOn);
    }

    /// <summary>
    /// An appointment somebody else shared is marked on the grid, and says whose it is when it is
    /// pointed at. The list beside the grid has always carried "Shared by anna" on its card; the grid
    /// said nothing at all, so a shared appointment read as one of the reader's own (reported
    /// 2026-09-20). A mark rather than the sentence, because a chip is one line in a seventh of a week.
    /// </summary>
    [Fact]
    public void An_event_somebody_shared_is_marked_on_the_month_grid()
    {
        var todayNoon = DateTime.SpecifyKind(DateTime.Today.AddHours(14).AddMinutes(30), DateTimeKind.Local);
        var shared = CreateTimedEvent(todayNoon, todayNoon.AddHours(1), "Dentist") with
        {
            IsShared = true,
            SharedByUserName = "anna"
        };
        RegisterCalendarApiClient([shared]);

        var cut = RenderComponent<Calendar>();

        var chip = cut.Find(".calendar-event-chip");
        Assert.Contains("calendar-chip-shared", chip.ClassName);
        Assert.Contains("anna", chip.GetAttribute("title"));
    }

    /// <summary>And the reader's own carries neither, which is what makes the mark worth anything.</summary>
    [Fact]
    public void An_event_of_your_own_is_not_marked_as_shared()
    {
        var todayNoon = DateTime.SpecifyKind(DateTime.Today.AddHours(14).AddMinutes(30), DateTimeKind.Local);
        RegisterCalendarApiClient([CreateTimedEvent(todayNoon, todayNoon.AddHours(1), "Dentist")]);

        var cut = RenderComponent<Calendar>();

        var chip = cut.Find(".calendar-event-chip");
        Assert.DoesNotContain("calendar-chip-shared", chip.ClassName);
        Assert.Equal("Dentist", chip.GetAttribute("title"));
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

    /// <summary>
    /// A tab narrows the grid and the list beside it together, so the whole calendar is what is in one
    /// folder rather than half of it. Only a folder somebody made narrows anything now - the tab the
    /// page opens on holds the lot, which is what All means since 2026-09-24.
    /// </summary>
    [Fact]
    public void Only_the_events_under_the_open_tab_are_on_the_calendar()
    {
        var week = Guid.NewGuid();
        RegisterFolders([new Orbit.Contracts.Folders.FolderDto(
            week, "This week", nameof(Orbit.Core.Folders.FolderScope.Calendar),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)]);
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        var filed = CreateTimedEvent(midMonth, midMonth.AddHours(1), "Dentist") with { FolderId = week };
        RegisterCalendarApiClient([filed, CreateTimedEvent(midMonth, midMonth.AddHours(1), "Haircut")]);

        var cut = RenderComponent<Calendar>();

        // All is where a page opens, and it holds both since 2026-09-24 - see FolderKey.Holds.
        Assert.Contains("Haircut", cut.Markup);
        Assert.Contains("Dentist", cut.Markup);

        cut.FindAll(".folder-tab").Single(tab => tab.TextContent.Contains("This week")).Click();

        Assert.Contains("Dentist", cut.Markup);
        Assert.DoesNotContain("Haircut", cut.Markup);
    }

    /// <summary>
    /// A deadline is drawn under the tab anything unfiled is drawn under, and under no other. It is an
    /// entry on a task list rather than an appointment: it has no folder of this page's, and nothing
    /// here can put one away. Every tab drew every deadline until 2026-09-25, so the Archived tab
    /// showed a page of things that had not been archived at all - reported that day, in the list and
    /// on the grid alike. See CalendarDeadlineTab, which the phone reads too.
    /// </summary>
    [Fact]
    public void A_deadline_is_only_on_the_tab_where_anything_unfiled_is()
    {
        var todayMorning = DateTime.SpecifyKind(DateTime.Today.AddHours(9), DateTimeKind.Local);
        RegisterCalendarApiClient([
            CreateTimedEvent(todayMorning, todayMorning.AddHours(1), "Dentist") with { IsArchived = true }]);
        RegisterTasksApiClient([CreateTaskListWithDueItem(todayMorning, "Send the report")]);

        var cut = RenderComponent<Calendar>();
        Assert.Contains("Send the", cut.Markup);

        cut.FindAll(".folder-tab").Single(tab => tab.TextContent.Contains("Archived")).Click();

        // The appointment that was put away is there; the deadline, which nobody put anywhere, is not.
        Assert.Contains("Dentist", cut.Markup);
        Assert.DoesNotContain("Send the", cut.Markup);
    }

    /// <summary>
    /// A chosen tag filter narrows the deadlines and leaves the appointments alone (asked for on
    /// 2026-09-24). Not a shortcut: a filter is made of the tags on task lists, and an appointment is on
    /// no list and carries none - see Calendar.DeadlinesToShow. And the page says on screen that it is
    /// narrowed, the menu it was chosen in being shut by then.
    /// </summary>
    [Fact]
    public void A_chosen_filter_narrows_the_deadlines_and_leaves_the_appointments()
    {
        var todayMorning = DateTime.SpecifyKind(DateTime.Today.AddHours(9), DateTimeKind.Local);
        RegisterCalendarApiClient([CreateTimedEvent(todayMorning, todayMorning.AddHours(1), "Dentist")]);
        var home = CreateTaskListWithDueItem(todayMorning, "Fix the shelf") with { Tags = ["home"] };
        var work = CreateTaskListWithDueItem(todayMorning, "Send the report") with { Tags = ["work"] };
        var filter = new TaskTagFilterDto(Guid.NewGuid(), ["home"], MatchesAll: false, DateTimeOffset.UtcNow);
        RegisterTasksApiClient([home, work], [filter]);

        var cut = RenderComponent<Calendar>();
        Assert.Contains("Send the report", cut.Markup);

        cut.Find(".overflow-menu-trigger").Click();
        cut.FindAll(".overflow-menu-dropdown .avatar-dropdown-item")
            .First(option => option.TextContent.Contains("home"))
            .Click();

        Assert.Contains("Fix the shelf", cut.Markup);
        Assert.DoesNotContain("Send the report", cut.Markup);
        // The appointment is untouched, and the page says which filter is narrowing it.
        Assert.Contains("Dentist", cut.Markup);
        Assert.Contains("Only the deadlines of lists tagged", cut.Markup);

        cut.FindAll("button").First(button => button.TextContent.Trim() == "Show everything").Click();

        Assert.Contains("Send the report", cut.Markup);
    }

    /// <summary>The calendar draws no Private tab - an event is never sealed, see FolderPages.HasAPrivateTab.</summary>
    [Fact]
    public void The_calendar_offers_no_Private_tab()
    {
        RegisterCalendarApiClient([]);

        var cut = RenderComponent<Calendar>();

        Assert.DoesNotContain(
            "Private",
            cut.FindAll(".folder-tab").Select(tab => tab.TextContent.Trim()));
    }

    /// <summary>The tabs this account has made, over the empty set OrbitTestContext registers.</summary>
    private void RegisterFolders(IReadOnlyList<Orbit.Contracts.Folders.FolderDto> folders)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(folders)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new FolderState(new FoldersApiClient(httpClient)));
    }

    /// <summary>Something that takes the whole of a day and so has no hour of its own.</summary>
    private static CalendarEventDto CreateAllDayEvent(DateTime localDay, string title)
        => CreateTimedEvent(localDay.Date, localDay.Date.AddDays(1), title) is var timed
            ? timed with { Details = timed.Details with { IsAllDay = true } }
            : throw new InvalidOperationException();

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

    /// <summary>
    /// The list said what was on the calendar and nothing about which of it somebody had just been told
    /// something about - so a reminder arrived, the bell counted it, and the list it was about looked
    /// exactly as it had a moment before. The same mark every other list in Orbit carries.
    /// </summary>
    [Fact]
    public void An_event_the_bell_is_talking_about_is_marked_on_the_list()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        var dentist = CreateTimedEvent(midMonth, midMonth.AddHours(1), "Dentist");
        RegisterCalendarApiClient([dentist, CreateTimedEvent(midMonth, midMonth.AddHours(1), "Haircut")]);
        SomethingUnreadAbout($"/calendar/{dentist.Id}");

        var cut = RenderComponent<Calendar>();

        var marked = cut.FindAll(".item-card-unseen").Select(card => card.TextContent).ToList();
        Assert.Contains(marked, text => text.Contains("Dentist", StringComparison.Ordinal));
        Assert.DoesNotContain(marked, text => text.Contains("Haircut", StringComparison.Ordinal));
    }

    /// <summary>Nothing waiting, nothing marked - the mark has to mean something to be worth having.</summary>
    [Fact]
    public void Nothing_is_marked_when_the_bell_is_holding_nothing()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClient([CreateTimedEvent(midMonth, midMonth.AddHours(1), "Dentist")]);

        var cut = RenderComponent<Calendar>();

        Assert.Empty(cut.FindAll(".item-card-unseen"));
    }

    /// <summary>
    /// A deadline is a task entry, and a reminder about one points at the list it is on - see
    /// Dashboard's UpcomingDeadlines, which addresses it the same way.
    /// </summary>
    [Fact]
    public void A_deadline_the_bell_is_talking_about_is_marked_too()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        var taskList = CreateTaskListWithDueItem(midMonth, "Pay the rent");
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([taskList]);
        SomethingUnreadAbout($"/tasks/{taskList.Id}");

        var cut = RenderComponent<Calendar>();

        Assert.Contains(
            cut.FindAll(".item-card-unseen"),
            card => card.TextContent.Contains("Pay the rent", StringComparison.Ordinal));
    }

    /// <summary>What the bell is holding in a given test - see NotificationFeedState.</summary>
    private void SomethingUnreadAbout(params string[] urls)
        => Services.GetRequiredService<NotificationFeedState>().Set(
            [.. urls.Select(url => new Orbit.Contracts.Notifications.NotificationEntryDto(
                Guid.NewGuid(), "EventReminder", "Coming up", "Dentist at 10:00.", url,
                DateTimeOffset.UtcNow, IsRead: false))]);

    private void RegisterCalendarApiClient(IReadOnlyList<CalendarEventDto> events)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(events))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new CalendarApiClient(httpClient));
    }

    /// <param name="tagFilters">
    /// The filters the account made, which the calendar has read since 2026-09-26 to offer narrowing its
    /// deadlines by them. Told apart from the lists by the address asked for: this stub answered every
    /// read with the task lists, and the page then read a list as a filter with no words in it - a fake
    /// answering something the server never would.
    /// </param>
    private void RegisterTasksApiClient(
        IReadOnlyList<TaskDto> taskLists, IReadOnlyList<TaskTagFilterDto>? tagFilters = null)
    {
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.Contains("/api/task-filters", StringComparison.Ordinal)
                ? JsonResponse(tagFilters ?? [])
                : JsonResponse(taskLists));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new TasksApiClient(httpClient));
    }

    /// <summary>
    /// The same calendar, on a server that answers every read and refuses every write. What a page has
    /// to be able to say something about: a press that changed nothing leaves the card exactly as it
    /// was, which is also what a press that never registered looks like.
    /// </summary>
    private void RegisterCalendarApiClientRefusingWrites(IReadOnlyList<CalendarEventDto> events)
    {
        var handler = new StubHttpMessageHandler(request => request.Method == HttpMethod.Get
            ? JsonResponse(events)
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new CalendarApiClient(httpClient));
    }

    private static HttpResponseMessage JsonResponse<TItem>(IReadOnlyList<TItem> items)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(items) };

    /// <summary>
    /// A press on a card that the server refuses is said out loud. This page had nowhere at all to say
    /// it - four actions logged the failure and returned, leaving the card exactly as it was - which is
    /// the rule Tasks.razor and Notes.razor already follow. 2026-09-19.
    /// </summary>
    [Fact]
    public void A_refused_press_on_a_card_is_said_out_loud()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClientRefusingWrites([CreateTimedEvent(midMonth, midMonth.AddHours(1), "Dentist")]);
        RegisterTasksApiClient([]);
        var cut = RenderComponent<Calendar>();
        Assert.Empty(cut.FindAll("p.error"));

        ChooseOnTheCard(cut, "Archive");

        Assert.Contains("Couldn't change that event", cut.Find("p.error").TextContent, StringComparison.Ordinal);
    }

    /// <summary>And a copy nobody could make says so in its own words rather than the archive's.</summary>
    [Fact]
    public void A_copy_the_server_refuses_says_so()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClientRefusingWrites([CreateTimedEvent(midMonth, midMonth.AddHours(1), "Dentist")]);
        RegisterTasksApiClient([]);
        var cut = RenderComponent<Calendar>();

        ChooseOnTheCard(cut, "Duplicate");

        Assert.Contains("Couldn't make a copy", cut.Find("p.error").TextContent, StringComparison.Ordinal);
    }

    /// <summary>Opens the menu on the one card on the list and presses the entry named.</summary>
    private static void ChooseOnTheCard(IRenderedFragment cut, string label)
    {
        cut.Find(".item-card-list .overflow-menu-trigger").Click();
        cut.FindAll(".item-card-list .avatar-dropdown-item")
            .First(item => item.TextContent.Trim() == label)
            .Click();
    }
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
    /// The same rule for an appointment a task list made, whenever it happens to fall. Its event has
    /// nothing to tick - only the entry behind it does - so the list had nothing asking the question and
    /// a ticked-off appointment stayed for as long as its date was still ahead, which is most of the time
    /// somebody would tick one off. Ticked off *and* still in the future is the case that showed it.
    /// </summary>
    [Fact]
    public void A_ticked_off_appointment_still_to_come_is_not_listed_until_everything_is_asked_for()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        var laterThisMonth = LastDayOfThisMonth().AddHours(10);
        var done = CreateTimedEvent(laterThisMonth, laterThisMonth.AddHours(1), "Already been");
        var stillToGo = CreateTimedEvent(laterThisMonth, laterThisMonth.AddHours(1), "Still to go");
        RegisterCalendarApiClient([done, stillToGo]);
        RegisterTasksApiClient([
            TickedOff(TaskListWithAnEntryFor(done.Id, laterThisMonth, "Already been")),
            TaskListWithAnEntryFor(stillToGo.Id, laterThisMonth, "Still to go")]);

        var cut = RenderComponent<Calendar>();

        Assert.Equal(["Still to go"], ListedNames(cut));

        ShowEverything(cut);

        Assert.Equal(["Already been", "Still to go"], ListedNames(cut));
    }

    /// <summary>
    /// And once it is asked for, it reads as done: struck through and greyed, the same mark a finished
    /// deadline carries, so the two look alike where the menu puts them side by side.
    /// </summary>
    [Fact]
    public void A_ticked_off_appointment_is_shown_struck_through()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        var laterThisMonth = LastDayOfThisMonth().AddHours(10);
        var done = CreateTimedEvent(laterThisMonth, laterThisMonth.AddHours(1), "Already been");
        RegisterCalendarApiClient([done]);
        RegisterTasksApiClient([TickedOff(TaskListWithAnEntryFor(done.Id, laterThisMonth, "Already been"))]);
        var cut = RenderComponent<Calendar>();

        ShowEverything(cut);

        Assert.Single(cut.FindAll(".item-card.item-card-done"));
    }

    /// <summary>
    /// The grid starts from the same answer as the list - what has been done leaves it - but is asked
    /// separately, from its own menu beside the view switch. It comes back struck through and greyed:
    /// an appointment whose entry is ticked off had no mark at all before, because an event has nothing
    /// to tick and only the entry behind it does, while a finished *deadline* has been struck through
    /// there all along.
    /// </summary>
    [Fact]
    public void A_ticked_off_appointment_leaves_the_month_grid_until_everything_is_asked_for()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        var laterThisMonth = LastDayOfThisMonth().AddHours(10);
        var done = CreateTimedEvent(laterThisMonth, laterThisMonth.AddHours(1), "Dentist");
        var stillToGo = CreateTimedEvent(laterThisMonth, laterThisMonth.AddHours(1), "Haircut");
        RegisterCalendarApiClient([done, stillToGo]);
        RegisterTasksApiClient([
            TickedOff(TaskListWithAnEntryFor(done.Id, laterThisMonth, "Dentist")),
            TaskListWithAnEntryFor(stillToGo.Id, laterThisMonth, "Haircut")]);

        var cut = RenderComponent<Calendar>();

        var chip = Assert.Single(cut.FindAll(".calendar-event-chip"));
        Assert.Contains("Haircut", chip.TextContent);

        ShowWhatIsDoneOnTheGrid(cut);

        var chips = cut.FindAll(".calendar-event-chip");
        Assert.Equal(2, chips.Count);
        var marked = Assert.Single(chips, entry => entry.ClassList.Contains("calendar-chip-done"));
        Assert.Contains("Dentist", marked.TextContent);
    }

    /// <summary>A ticked-off deadline is the same fact about the other kind of thing, and leaves too.</summary>
    [Fact]
    public void A_ticked_off_deadline_leaves_the_month_grid_too()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([
            CreateTaskListWithDueItem(midMonth, "Shopping"),
            TickedOff(CreateTaskListWithDueItem(midMonth, "Laundry"))]);

        var cut = RenderComponent<Calendar>();

        var chip = Assert.Single(cut.FindAll(".calendar-task-chip"));
        Assert.Contains("Shopping", chip.TextContent);

        ShowWhatIsDoneOnTheGrid(cut);

        Assert.Equal(2, cut.FindAll(".calendar-task-chip").Count);
    }

    /// <summary>
    /// And the two switches are separate, which is what the user asked for on 2026-09-20: one menu used
    /// to govern both, so a reader who wanted finished work in the list got it drawn over the month as
    /// well. Asking the list changes nothing on the grid, and asking the grid changes nothing in the
    /// list.
    /// </summary>
    [Fact]
    public void The_list_and_the_grid_are_asked_for_finished_work_separately()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([
            CreateTaskListWithDueItem(midMonth, "Shopping"),
            TickedOff(CreateTaskListWithDueItem(midMonth, "Laundry"))]);

        var cut = RenderComponent<Calendar>();

        ShowEverything(cut);

        Assert.Contains("Laundry", ListedNames(cut));
        var chip = Assert.Single(cut.FindAll(".calendar-task-chip"));
        Assert.Contains("Shopping", chip.TextContent);

        ShowEverything(cut);
        ShowWhatIsDoneOnTheGrid(cut);

        Assert.DoesNotContain("Laundry", ListedNames(cut));
        Assert.Equal(2, cut.FindAll(".calendar-task-chip").Count);
    }

    /// <summary>
    /// The one place the grid parts company with the list, and it is deliberate. The list answers "what
    /// is coming", so an event that has ended stops being its subject; a grid is a picture of the
    /// period, and a month with every past day empty would be a month that had not happened. Done is a
    /// different fact from past, and only the first is somebody saying they are finished with it.
    /// </summary>
    [Fact]
    public void An_event_that_has_merely_ended_is_still_drawn_on_the_grid()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        var earlierToday = DateTime.Today;
        RegisterCalendarApiClient([CreateTimedEvent(earlierToday, earlierToday, "Finished")]);
        RegisterTasksApiClient([]);

        var cut = RenderComponent<Calendar>();

        // Gone from the list beside it, which is what that list is for...
        Assert.Empty(ListedNames(cut));
        // ...and still on the grid, which is a picture of the month rather than of what is left.
        Assert.Contains("Finished", Assert.Single(cut.FindAll(".calendar-event-chip")).TextContent);
    }

    /// <summary>
    /// The first hop of the case that was reported. Pressing a deadline here opens it as its entry on a
    /// task list, and the form beyond that used to end on /tasks - so the calendar says where it is,
    /// and the address is carried the rest of the way. See ReturnTo.
    /// </summary>
    [Fact]
    public void Opening_a_deadline_says_the_calendar_is_where_to_come_back_to()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([CreateTaskListWithDueItem(midMonth, "Buy milk")]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Calendar>();

        cut.Find(".item-card-name").Click();

        Assert.Contains($"{ReturnTo.QueryName}=%2Fcalendar", navigationManager.Uri);
    }

    /// <summary>
    /// And it opens as the entry, whether or not that entry is anywhere. It used to fork on that: a
    /// deadline with a place opened as itself and one without opened as the *list* it sits on, so the
    /// same press on the same list of cards meant two different objects, decided by a field no card
    /// mentions. Ticking it off is still done on the list, which the entry's page leads to.
    /// </summary>
    [Fact]
    public void A_deadline_with_nowhere_to_be_opens_as_the_entry_too()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        var taskList = CreateTaskListWithDueItem(midMonth, "Buy milk");
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([taskList]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Calendar>();

        cut.Find(".item-card-name").Click();

        // The return address says which day and view it was opened from - see HereAndNow.
        Assert.Contains($"/tasks/{taskList.Id}/items/{taskList.Items[0].Id}?", navigationManager.Uri);
        Assert.Contains($"{ReturnTo.QueryName}=", navigationManager.Uri);
    }

    /// <summary>The guard on both: an appointment nobody has ticked off is listed as it always was.</summary>
    [Fact]
    public void An_appointment_still_outstanding_is_listed_as_it_always_was()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        var laterThisMonth = LastDayOfThisMonth().AddHours(10);
        var calendarEvent = CreateTimedEvent(laterThisMonth, laterThisMonth.AddHours(1), "Dentist");
        RegisterCalendarApiClient([calendarEvent]);
        RegisterTasksApiClient([TaskListWithAnEntryFor(calendarEvent.Id, laterThisMonth, "Dentist")]);

        var cut = RenderComponent<Calendar>();

        Assert.Equal(["Dentist"], ListedNames(cut));
        Assert.Empty(cut.FindAll(".item-card-done"));
    }

    /// <summary>
    /// Late enough in this month to be still ahead whenever the suite runs, and still inside the period
    /// the list is showing - a time relative to "now" would fall outside it on the last day of a month.
    /// </summary>
    private static DateTime LastDayOfThisMonth()
        => new(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month));

    private static void ShowEverything(IRenderedFragment cut)
        => PressMenuEntry(cut, ".page-header-actions", "Everything");

    /// <summary>
    /// The grid's own entry, in the menu beside the view switch rather than in the page header's. The
    /// two are separate switches - see Calendar.GridShowsWhatIsDone.
    /// </summary>
    private static void ShowWhatIsDoneOnTheGrid(IRenderedFragment cut)
        => PressMenuEntry(cut, ".calendar-visualization-toolbar-views", "already done");

    /// <summary>
    /// Presses one entry of a menu, opening it first if it is shut. Both of these menus hold settings
    /// and so stay open behind the entry - pressing the trigger again would close them, which is how a
    /// test that asks for the same thing twice used to find an empty menu.
    /// </summary>
    private static void PressMenuEntry(IRenderedFragment cut, string within, string text)
    {
        if (cut.FindAll($"{within} .overflow-menu-dropdown").Count == 0)
        {
            cut.Find($"{within} .overflow-menu-trigger").Click();
        }

        cut.FindAll($"{within} .avatar-dropdown-item")
            .First(entry => entry.TextContent.Contains(text, StringComparison.Ordinal))
            .Click();
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

        // The return address says which day and view it was opened from - see HereAndNow.
        Assert.Contains($"/tasks/{taskList.Id}/items/{taskList.Items[0].Id}?", navigationManager.Uri);
        Assert.Contains($"{ReturnTo.QueryName}=", navigationManager.Uri);
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

        // With the day and view it was opened from on the end of it - see HereAndNow.
        Assert.Contains($"/calendar/{calendarEvent.Id}?", navigationManager.Uri);
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

    /// <summary>
    /// An event somebody shared with this reader is taken off their own calendar rather than deleted -
    /// the server drops their grant and the owner keeps the event. It used to offer nothing at all: a
    /// shared event cannot be archived either, and Delete is only offered once something has been, so
    /// one that arrived could not be got rid of by any press on this page.
    /// </summary>
    [Fact]
    public void A_shared_event_is_taken_off_your_own_calendar_rather_than_deleted()
    {
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClient([
            CreateTimedEvent(midMonth, midMonth.AddHours(1), "Their meeting") with
            {
                IsShared = true,
                SharedByUserName = "bob"
            }]);
        RegisterTasksApiClient([]);
        var cut = RenderComponent<Calendar>();

        cut.FindAll(".item-card .overflow-menu-trigger").First().Click();

        var offered = cut.Find(".item-card-menu").TextContent;
        Assert.Contains("Remove from my list", offered);
        Assert.DoesNotContain("Delete", offered);
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

    /// <summary>
    /// A deadline on a list its owner has closed is done, whatever its own tick says. Marking a list
    /// finished with work still on it is a way of saying "no more of this" (TaskList.IsMarkedCompleted),
    /// and the calendar would otherwise keep the deadlines it was closed to be rid of.
    /// </summary>
    [Fact]
    public void A_deadline_on_a_list_its_owner_closed_reads_as_done()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        var midMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 0, 0);
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([
            CreateTaskListWithDueItem(midMonth, "Still to do"),
            CreateTaskListWithDueItem(midMonth, "On a closed list")
                with { IsCompleted = true, Completion = nameof(TaskListCompletion.Finished) }]);

        var cut = RenderComponent<Calendar>();

        Assert.Equal(["Still to do"], ListedNames(cut));
    }

    /// <summary>
    /// A week is seven of the day view's timelines side by side, so what it draws is placed by when it
    /// happens - what is its own is which seven days those are.
    /// </summary>
    [Fact]
    public void The_week_view_lists_its_own_week_and_not_the_month_around_it()
    {
        var monday = CalendarGridBuilder.StartOfWeek(DateOnly.FromDateTime(DateTime.Today));
        var thisWeek = monday.AddDays(2).ToDateTime(new TimeOnly(10, 0));
        var nextWeek = monday.AddDays(9).ToDateTime(new TimeOnly(10, 0));
        RegisterCalendarApiClient([
            CreateTimedEvent(thisWeek, thisWeek.AddHours(1), "This week"),
            CreateTimedEvent(nextWeek, nextWeek.AddHours(1), "Next week")]);
        RegisterTasksApiClient([]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Week").Click();

        // Seven columns, one per day - see CalendarGridBuilder.BuildWeekTimeline.
        Assert.Equal(7, cut.FindAll(".calendar-week-grid-day").Count);
        Assert.Equal(["This week"], ListedNames(cut));
    }

    /// <summary>
    /// And placed by when it happens rather than listed. A block's top is the percentage of the day its
    /// start falls at, which is what lets a reader put it at the half hour by eye - 10:30 is 43.75% of
    /// the way down a 24-hour column.
    /// </summary>
    [Fact]
    public void An_appointment_sits_at_the_height_of_the_hour_it_starts_at()
    {
        var monday = CalendarGridBuilder.StartOfWeek(DateOnly.FromDateTime(DateTime.Today));
        var halfPastTen = monday.AddDays(2).ToDateTime(new TimeOnly(10, 30));
        RegisterCalendarApiClient([CreateTimedEvent(halfPastTen, halfPastTen.AddHours(1), "Dentist")]);
        RegisterTasksApiClient([]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Week").Click();

        var block = cut.FindAll(".calendar-week-grid-day .calendar-event-block")
            .Single(drawn => drawn.TextContent.Contains("Dentist", StringComparison.Ordinal));
        Assert.Contains("top:43.75%", block.GetAttribute("style"), StringComparison.Ordinal);
    }

    /// <summary>
    /// A whole-day thing has no hour to be drawn at, so it goes in a band across the top rather than
    /// being given midnight, which is a lie about when it is.
    /// </summary>
    [Fact]
    public void A_whole_day_appointment_sits_in_the_band_above_the_hours()
    {
        var monday = CalendarGridBuilder.StartOfWeek(DateOnly.FromDateTime(DateTime.Today));
        RegisterCalendarApiClient([CreateAllDayEvent(monday.AddDays(1).ToDateTime(TimeOnly.MinValue), "Bank holiday")]);
        RegisterTasksApiClient([]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Week").Click();

        var band = cut.Find(".calendar-week-grid-all-day");
        Assert.Contains("Bank holiday", band.TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".calendar-week-grid-day .calendar-event-block"));
    }

    /// <summary>Pressing a day's name opens that day, the way pressing a cell of the month grid does.</summary>
    [Fact]
    public void Pressing_a_day_name_in_the_week_opens_that_day()
    {
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([]);
        var cut = RenderComponent<Calendar>();
        FindViewSwitchButton(cut, "Week").Click();

        cut.FindAll(".calendar-week-grid-day-name").Skip(3).First().Click();

        Assert.NotEmpty(cut.FindAll(".calendar-day-grid"));
    }

    /// <summary>
    /// What is over is left out of every view until the reader asks for it - the day and the week
    /// included. They used to show it whatever the menu said, on the reasoning that opening one
    /// particular day is asking what happened on it; the user asked for the choice back on 2026-09-18,
    /// because a mark that is ticked and cannot be unticked is a control that refuses.
    /// </summary>
    [Fact]
    public void The_day_view_leaves_out_what_is_finished_until_the_reader_asks_for_it()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        var todayMorning = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 10, 0, 0);
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([
            CreateTaskListWithDueItem(todayMorning, "Still to do"),
            TickedOff(CreateTaskListWithDueItem(todayMorning, "Already done"))]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Day").Click();
        Assert.Equal(["Still to do"], ListedNames(cut));

        ShowEverything(cut);

        Assert.Equal(["Still to do", "Already done"], ListedNames(cut));
    }

    /// <summary>
    /// And the menu entry can be pressed in every view, in both directions - it was ticked and greyed
    /// in the day and the week, with no way back to what is still to come.
    /// </summary>
    [Fact]
    public void The_menu_entry_is_the_readers_in_the_day_view_too()
    {
        Services.AddSingleton(new CalendarListOrder(new StubJSRuntime()));
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Day").Click();

        Assert.DoesNotContain("chosen", EverythingEntry(cut).ClassList);
        Assert.False(EverythingEntry(cut).HasAttribute("disabled"));

        // The menu is already open, so this presses the entry rather than the trigger again.
        EverythingEntry(cut).Click();

        Assert.Contains("chosen", EverythingEntry(cut).ClassList);
    }

    /// <summary>The menu entry that asks for what is over, which exists only once the menu is open.</summary>
    private static IElement EverythingEntry(IRenderedFragment cut)
    {
        if (cut.FindAll(".page-header-actions .avatar-dropdown-item").Count == 0)
        {
            cut.Find(".page-header-actions .overflow-menu-trigger").Click();
        }

        return cut.FindAll(".page-header-actions .avatar-dropdown-item")
            .First(item => item.TextContent.Contains("Everything", StringComparison.Ordinal));
    }


    /// <summary>
    /// A link can name the view and the day, which is how the dashboard's summary of today arrives at
    /// today rather than at the month today is in - see Dashboard.razor's GoToCalendar.
    /// </summary>
    [Fact]
    public void A_link_can_ask_for_one_particular_day()
    {
        var yesterday = DateTime.Today.AddDays(-1);
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([]);
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"https://example.test/calendar?view=day&on={yesterday:yyyy-MM-dd}");

        var cut = RenderComponent<Calendar>();

        Assert.Equal("true", FindViewSwitchButton(cut, "Day").GetAttribute("aria-pressed"));
        // In the culture the page writes dates in, not the one the machine running the test happens to
        // be set to - the label is built with Translations.DisplayCulture, so a Polish Mac reading an
        // English page would otherwise be comparing "8 września" against "8 September".
        var displayCulture = Services.GetRequiredService<Translations>().DisplayCulture;
        Assert.Contains(
            yesterday.ToString("d MMMM yyyy", displayCulture),
            cut.Find(".calendar-period-label").TextContent);
    }

    /// <summary>
    /// And is obeyed once. Without that, pressing Month on a page reached by such a link puts the view
    /// straight back: the parameters are still what they were, and nothing tells "asked again" from
    /// "still there".
    /// </summary>
    [Fact]
    public void A_link_that_asked_for_a_day_does_not_keep_asking()
    {
        RegisterCalendarApiClient([]);
        RegisterTasksApiClient([]);
        Services.GetRequiredService<NavigationManager>().NavigateTo("https://example.test/calendar?view=day");
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Month").Click();

        Assert.Equal("true", FindViewSwitchButton(cut, "Month").GetAttribute("aria-pressed"));
    }
}
