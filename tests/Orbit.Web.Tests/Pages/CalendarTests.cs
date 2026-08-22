using System.Net;
using System.Net.Http.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Chat;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

public sealed class CalendarTests : TestContext
{
    public CalendarTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterChatApiClient([]);
        // Only reached if an event carries a guest id missing from the (empty) contact list above - none
        // of these tests add guests, so this just needs to satisfy Calendar.razor's @inject.
        Services.AddSingleton(new UsersApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") }));
    }

    [Fact]
    public void The_calendar_opens_in_month_view_with_the_Miesiac_button_marked_active()
    {
        RegisterCalendarApiClient([]);

        var cut = RenderComponent<Calendar>();

        Assert.NotEmpty(cut.FindAll(".calendar-month-grid"));
        Assert.Equal("true", FindViewSwitchButton(cut, "Miesiąc").GetAttribute("aria-pressed"));
        Assert.Equal("false", FindViewSwitchButton(cut, "Dzień").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Clicking_Dzien_switches_the_visualization_to_the_day_grid()
    {
        RegisterCalendarApiClient([]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Dzień").Click();

        Assert.NotEmpty(cut.FindAll(".calendar-day-grid"));
        Assert.Empty(cut.FindAll(".calendar-month-grid"));
        Assert.Equal("true", FindViewSwitchButton(cut, "Dzień").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Clicking_Rok_switches_the_visualization_to_a_year_grid_with_all_12_months()
    {
        RegisterCalendarApiClient([]);
        var cut = RenderComponent<Calendar>();

        FindViewSwitchButton(cut, "Rok").Click();

        Assert.Equal(12, cut.FindAll(".calendar-year-grid-month").Count);
    }

    [Fact]
    public void Hiding_the_event_list_removes_the_panel_and_relabels_the_toggle_button()
    {
        RegisterCalendarApiClient([]);
        var cut = RenderComponent<Calendar>();
        Assert.NotEmpty(cut.FindAll(".calendar-event-list-panel"));

        FindButtonByText(cut, "Ukryj listę").Click();

        Assert.Empty(cut.FindAll(".calendar-event-list-panel"));
        Assert.Contains(cut.FindAll("button"), button => button.TextContent == "Pokaż listę");
        // The visualization panel keeps rendering full-width once the list is hidden.
        Assert.NotEmpty(cut.FindAll(".calendar-visualization-panel"));
    }

    [Fact]
    public void Todays_timed_event_shows_up_as_a_chip_with_its_start_time_in_the_month_view()
    {
        var todayNoon = DateTime.SpecifyKind(DateTime.Today.AddHours(14).AddMinutes(30), DateTimeKind.Local);
        var calendarEvent = CreateTimedEvent(todayNoon, todayNoon.AddHours(1), "Spotkanie zespołu");
        RegisterCalendarApiClient([calendarEvent]);

        var cut = RenderComponent<Calendar>();

        var chipText = cut.Find(".calendar-event-chip").TextContent;
        Assert.Contains("14:30", chipText);
        Assert.Contains("Spotkanie zespołu", chipText);
    }

    private static IElement FindViewSwitchButton(IRenderedComponent<Calendar> cut, string label)
        => cut.Find(".calendar-view-switch").QuerySelectorAll("button").Single(button => button.TextContent == label);

    private static IElement FindButtonByText(IRenderedComponent<Calendar> cut, string text)
        => cut.FindAll("button").Single(button => button.TextContent == text);

    private static CalendarEventDto CreateTimedEvent(DateTime localStart, DateTime localEnd, string title)
        => new(
            Guid.NewGuid(),
            new CalendarEventDetailsDto(
                title, null, null, null,
                new DateTimeOffset(DateTime.SpecifyKind(localStart, DateTimeKind.Local)),
                new DateTimeOffset(DateTime.SpecifyKind(localEnd, DateTimeKind.Local)),
                IsAllDay: false, null, [], [], "None", "None"),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, IsShared: false, SharedByUserName: null);

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

    private static HttpResponseMessage JsonResponse<TItem>(IReadOnlyList<TItem> items)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(items) };
}
