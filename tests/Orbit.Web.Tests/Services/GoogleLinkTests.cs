using System.Globalization;
using Orbit.Contracts.Calendar;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Covers the URLs Orbit hands to Google. They are the whole feature - there is no API call behind them -
/// so a wrong date shape or an unescaped character is the difference between a link that works and one
/// that opens an empty form.
/// </summary>
public sealed class GoogleLinkTests
{
    private static readonly DateTimeOffset TenAm = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_event_link_carries_the_title_and_both_instants()
    {
        var link = GoogleCalendarEventLink.ForEvent("Dentist", TenAm, TenAm.AddHours(1));

        // Google's template links accept exactly this shape and silently ignore anything else.
        Assert.Contains("text=Dentist", link);
        Assert.Contains("dates=20260901T100000Z/20260901T110000Z", link);
    }

    [Fact]
    public void An_event_link_converts_a_local_time_to_utc()
    {
        // A calendar stores instants, and Google reads the Z form as UTC - handing it a local time
        // unchanged would move the event by the offset.
        var warsawMorning = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(2));

        var link = GoogleCalendarEventLink.ForEvent("Dentist", warsawMorning, warsawMorning.AddHours(1));

        Assert.Contains("dates=20260901T080000Z/20260901T090000Z", link);
    }

    [Fact]
    public void An_all_day_event_uses_dates_and_an_exclusive_end()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            "Holiday", TenAm, TenAm.AddHours(8), isAllDay: true);

        // Google reads the end of an all-day range as exclusive, so a single day is written as 1st/2nd -
        // passing the same date twice would produce an event of no length.
        Assert.Contains("dates=20260901/20260902", link);
    }

    [Fact]
    public void An_all_day_event_spanning_days_keeps_its_own_end()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            "Trip", TenAm, TenAm.AddDays(3), isAllDay: true);

        Assert.Contains("dates=20260901/20260904", link);
    }

    [Fact]
    public void Everything_that_goes_into_a_link_is_escaped()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            "Coffee & cake", TenAm, TenAm.AddHours(1),
            description: "Bring the tickets?", location: "Rynek Główny 1, Kraków");

        // An unescaped & would end the title and invent a parameter; a raw ? or space breaks the URL.
        Assert.DoesNotContain("text=Coffee & cake", link);
        Assert.Contains("Coffee+%26+cake", link);
        Assert.Contains("tickets%3f", link, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Krak%c3%b3w", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_event_link_leaves_out_details_and_location_when_there_are_none()
    {
        var link = GoogleCalendarEventLink.ForEvent("Dentist", TenAm, TenAm.AddHours(1));

        Assert.DoesNotContain("details=", link);
        Assert.DoesNotContain("location=", link);
    }

    [Fact]
    public void A_task_link_ends_at_the_deadline_and_says_where_it_came_from()
    {
        var link = GoogleCalendarEventLink.ForTaskItem("File the return", TenAm, "Admin");

        // Google Calendar template links can't create a task, so the deadline becomes a short event
        // ending at it - which is what a deadline in a calendar looks like.
        Assert.Contains("text=File+the+return", link);
        Assert.Contains("dates=20260901T093000Z/20260901T100000Z", link);
        Assert.Contains("Admin", link);
    }

    [Fact]
    public void A_place_link_points_at_coordinates()
    {
        var link = GoogleMapsLink.ToPlace(50.0617, 19.9373);

        Assert.Equal("https://www.google.com/maps/search/?api=1&query=50.0617,19.9373", link);
    }

    [Fact]
    public void Coordinates_never_use_a_decimal_comma()
    {
        // Under pl-PL a double formats as "50,0617", which would split the pair into two parameters and
        // send the reader somewhere else entirely.
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
        try
        {
            Assert.Contains("query=50.0617,19.9373", GoogleMapsLink.ToPlace(50.0617, 19.9373));
            Assert.Contains("destination=50.0617,19.9373", GoogleMapsLink.ToDirections(50.0617, 19.9373));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void A_place_link_can_take_an_address_instead()
    {
        var link = GoogleMapsLink.ToPlace("Rynek Główny 1, Kraków");

        Assert.StartsWith("https://www.google.com/maps/search/?api=1&query=", link);
        Assert.DoesNotContain(" ", link);
    }

    [Fact]
    public void Directions_name_only_the_destination()
    {
        var link = GoogleMapsLink.ToDirections(50.0617, 19.9373);

        // No origin on purpose: Google routes from where the reader actually is. Handing it Orbit's
        // recorded position would look more precise and be worse - that point is whatever they last
        // recorded deliberately, which may be another city days ago.
        Assert.Equal("https://www.google.com/maps/dir/?api=1&destination=50.0617,19.9373", link);
    }

    [Fact]
    public void A_repeating_event_carries_its_rule()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            "Standup", TenAm, TenAm.AddMinutes(15), recurrence: new RecurrenceDto("Daily", 1, null));

        // Google reads an iCalendar RRULE here; anything it does not recognise it drops silently, which
        // is why the shape matters more than usual.
        Assert.Contains("recur=RRULE%3aFREQ%3dDAILY", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_interval_of_one_is_left_unsaid()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            "Standup", TenAm, TenAm.AddMinutes(15), recurrence: new RecurrenceDto("Weekly", 1, null));

        // INTERVAL=1 is the default, so saying it adds length and nothing else.
        Assert.DoesNotContain("INTERVAL", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_other_week_says_so()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            "Retro", TenAm, TenAm.AddHours(1), recurrence: new RecurrenceDto("Weekly", 2, null));

        Assert.Contains("FREQ%3dWEEKLY%3bINTERVAL%3d2", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_end_date_travels_as_an_instant_in_utc()
    {
        var until = new DateTimeOffset(2026, 12, 31, 23, 0, 0, TimeSpan.FromHours(2));
        var link = GoogleCalendarEventLink.ForEvent(
            "Course", TenAm, TenAm.AddHours(1), recurrence: new RecurrenceDto("Monthly", 1, until));

        // 23:00 in Warsaw is 21:00 UTC - handing Google the local time would end the series two hours late.
        Assert.Contains("UNTIL%3d20261231T210000Z", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_event_that_does_not_repeat_says_nothing_about_repeating()
    {
        var link = GoogleCalendarEventLink.ForEvent("Dentist", TenAm, TenAm.AddHours(1));

        Assert.DoesNotContain("recur", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_frequency_this_version_does_not_know_is_left_out_rather_than_guessed()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            "Something", TenAm, TenAm.AddHours(1), recurrence: new RecurrenceDto("Fortnightly", 1, null));

        // A rule Google cannot parse makes it drop the recurrence silently; one occurrence is a better
        // wrong answer than a link that opens an empty form.
        Assert.DoesNotContain("recur", link, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("text=Something", link);
    }

    [Fact]
    public void Guests_are_left_out_of_the_link()
    {
        var link = GoogleCalendarEventLink.ForEvent("Dinner", TenAm, TenAm.AddHours(2), description: "With the team");

        // Google takes guests as "&add=<address>", which would put other people's email addresses into
        // a URL to save them a step they can do in Google's own form.
        Assert.DoesNotContain("add=", link, StringComparison.OrdinalIgnoreCase);
    }
}
