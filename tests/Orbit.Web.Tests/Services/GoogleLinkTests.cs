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
        var link = GoogleCalendarEventLink.ForEvent(new GoogleCalendarEvent("Dentist", TenAm, TenAm.AddHours(1)));

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

        var link = GoogleCalendarEventLink.ForEvent(new GoogleCalendarEvent("Dentist", warsawMorning, warsawMorning.AddHours(1)));

        Assert.Contains("dates=20260901T080000Z/20260901T090000Z", link);
    }

    /// <summary>
    /// An all-day event is stored as the instant local midnight began - see
    /// EventFormModel.ToDateTimeOffset - so these build theirs the same way rather than from a time of
    /// day, which is what a real one looks like and the only shape the bug below shows up in.
    /// </summary>
    private static DateTimeOffset LocalMidnightOn(int day)
        => new(new DateTime(2026, 9, day, 0, 0, 0, DateTimeKind.Local));

    [Fact]
    public void An_all_day_event_uses_dates_and_an_exclusive_end()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Holiday", LocalMidnightOn(1), LocalMidnightOn(1)) { IsAllDay = true });

        // Google reads the end of an all-day range as exclusive, so a single day is written as 1st/2nd -
        // passing the same date twice would produce an event of no length.
        Assert.Contains("dates=20260901/20260902", link);
    }

    /// <summary>
    /// Orbit's end date is the last day the event covers - that is what the calendar draws, see
    /// CalendarGridBuilder.OccursOnDate, which includes it - and Google's is the first day it does not.
    /// A trip from the 1st to the 4th is four days in the grid, so it has to be four in the link.
    /// Passing a multi-day end through unchanged made every such event a day short of what Orbit showed.
    /// </summary>
    [Fact]
    public void An_all_day_event_spanning_days_covers_the_same_days_the_grid_draws()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Trip", LocalMidnightOn(1), LocalMidnightOn(4)) { IsAllDay = true });

        Assert.Contains("dates=20260901/20260905", link);
    }

    /// <summary>
    /// Said as the two agreeing rather than as a string: the days the grid puts the event on, and the
    /// days Google's half-open range covers, have to be the same set.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(14, 16)]
    public void The_link_covers_exactly_the_days_the_grid_puts_it_on(int firstDay, int lastDay)
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Trip", LocalMidnightOn(firstDay), LocalMidnightOn(lastDay)) { IsAllDay = true });

        // What the grid draws: every day from the first to the last, the last included.
        var daysDrawn = lastDay - firstDay + 1;
        // What the link says: Google's end is the first day not covered, so the span is the difference.
        var googleEnd = DaysInTheLink(link).End;
        Assert.Equal(daysDrawn, googleEnd - firstDay);
    }

    private static (int Start, int End) DaysInTheLink(string link)
    {
        var range = link.Split("dates=")[1].Split('&')[0].Split('/');
        return (int.Parse(range[0][6..], CultureInfo.InvariantCulture), int.Parse(range[1][6..], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The day is the reader's, not UTC's. Local midnight anywhere east of Greenwich is the previous day
    /// in UTC, so reading the day off the UTC instant handed Google a holiday on the 14th as the 13th -
    /// and an all-day event has no time for Google to correct it by.
    /// </summary>
    [Fact]
    public void An_all_day_event_falls_on_the_day_the_reader_chose()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Holiday", LocalMidnightOn(14), LocalMidnightOn(14)) { IsAllDay = true });

        Assert.Contains("dates=20260914/20260915", link);
    }

    /// <summary>
    /// A timed event has no such problem and must not be "fixed" into one: it travels as an instant, and
    /// the Z is what tells Google which one - so the same local midnight stays an instant here.
    /// </summary>
    [Fact]
    public void A_timed_event_still_travels_as_an_instant()
    {
        var midnight = LocalMidnightOn(14);

        var link = GoogleCalendarEventLink.ForEvent(new GoogleCalendarEvent("Dentist", midnight, midnight.AddHours(1)));

        Assert.Contains($"dates={midnight.UtcDateTime:yyyyMMdd'T'HHmmss}Z/", link);
    }

    [Fact]
    public void Everything_that_goes_into_a_link_is_escaped()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Coffee & cake", TenAm, TenAm.AddHours(1))
            {
                Description = "Bring the tickets?",
                Location = "Rynek Główny 1, Kraków"
            });

        // An unescaped & would end the title and invent a parameter; a raw ? or space breaks the URL.
        Assert.DoesNotContain("text=Coffee & cake", link);
        Assert.Contains("Coffee+%26+cake", link);
        Assert.Contains("tickets%3f", link, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Krak%c3%b3w", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_event_link_leaves_out_details_and_location_when_there_are_none()
    {
        var link = GoogleCalendarEventLink.ForEvent(new GoogleCalendarEvent("Dentist", TenAm, TenAm.AddHours(1)));

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
            new GoogleCalendarEvent("Standup", TenAm, TenAm.AddMinutes(15)) { Recurrence = new RecurrenceDto("Daily", 1, null) });

        // Google reads an iCalendar RRULE here; anything it does not recognise it drops silently, which
        // is why the shape matters more than usual.
        Assert.Contains("recur=RRULE%3aFREQ%3dDAILY", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_interval_of_one_is_left_unsaid()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Standup", TenAm, TenAm.AddMinutes(15)) { Recurrence = new RecurrenceDto("Weekly", 1, null) });

        // INTERVAL=1 is the default, so saying it adds length and nothing else.
        Assert.DoesNotContain("INTERVAL", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_other_week_says_so()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Retro", TenAm, TenAm.AddHours(1)) { Recurrence = new RecurrenceDto("Weekly", 2, null) });

        Assert.Contains("FREQ%3dWEEKLY%3bINTERVAL%3d2", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_end_date_travels_as_an_instant_in_utc()
    {
        var until = new DateTimeOffset(2026, 12, 31, 23, 0, 0, TimeSpan.FromHours(2));
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Course", TenAm, TenAm.AddHours(1)) { Recurrence = new RecurrenceDto("Monthly", 1, until) });

        // 23:00 in Warsaw is 21:00 UTC - handing Google the local time would end the series two hours late.
        Assert.Contains("UNTIL%3d20261231T210000Z", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_event_that_does_not_repeat_says_nothing_about_repeating()
    {
        var link = GoogleCalendarEventLink.ForEvent(new GoogleCalendarEvent("Dentist", TenAm, TenAm.AddHours(1)));

        Assert.DoesNotContain("recur", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_frequency_this_version_does_not_know_is_left_out_rather_than_guessed()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Something", TenAm, TenAm.AddHours(1)) { Recurrence = new RecurrenceDto("Fortnightly", 1, null) });

        // A rule Google cannot parse makes it drop the recurrence silently; one occurrence is a better
        // wrong answer than a link that opens an empty form.
        Assert.DoesNotContain("recur", link, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("text=Something", link);
    }

    [Fact]
    public void An_event_with_no_guests_says_nothing_about_guests()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Dinner", TenAm, TenAm.AddHours(2)) { Description = "With the team" });

        Assert.DoesNotContain("add=", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Guests_travel_as_one_add_each()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Dinner", TenAm, TenAm.AddHours(2))
            {
                GuestEmailAddresses = ["anna@example.com", "bea@example.com"]
            });

        Assert.Contains("add=anna%40example.com", link, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("add=bea%40example.com", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Only_the_first_line_of_a_name_can_be_the_title()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Dentist\nBring the x-rays\nSecond floor", TenAm, TenAm.AddHours(1)));

        // Google's title is one line. The rest is not dropped - it goes where a multi-line thing can
        // live, which is the description.
        Assert.Contains("text=Dentist&", link);
        Assert.Contains("Bring+the+x-rays", link);
        Assert.Contains("Second+floor", link);
    }

    [Fact]
    public void A_names_extra_lines_come_before_the_description()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Dentist\nBring the x-rays", TenAm, TenAm.AddHours(1)) { Description = "Ask about the bill" });

        var details = link.Split("details=")[1].Split('&')[0];
        Assert.True(
            details.IndexOf("Bring", StringComparison.OrdinalIgnoreCase)
                < details.IndexOf("Ask", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void An_appointment_a_list_raised_says_which_list()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Dentist", TenAm, TenAm.AddHours(1)) { TaskListTitle = "Health" });

        // In a calendar full of other people's events, "Dentist" on its own does not say where it came
        // from - see LinkCalendarEventToTaskListCommand.
        Assert.Contains("text=Health+-+Dentist", link);
    }

    [Fact]
    public void Reminders_travel_as_words_because_a_template_link_has_no_field_for_them()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Dentist", TenAm, TenAm.AddHours(1))
            {
                ReminderMinutesBeforeStart = [30, 1440],
                NotifyAtStart = true
            });

        // Google's own form takes no reminders, so what Orbit would have sent is said in the
        // description rather than lost on the way over.
        Assert.Contains("30+min+before", link);
        Assert.Contains("1+days+before", link);
        Assert.Contains("at+the+start", link);
    }

    [Fact]
    public void An_event_nobody_asked_to_be_reminded_of_says_nothing_about_reminders()
    {
        var link = GoogleCalendarEventLink.ForEvent(new GoogleCalendarEvent("Dentist", TenAm, TenAm.AddHours(1)));

        Assert.DoesNotContain("reminder", link, StringComparison.OrdinalIgnoreCase);
    }
}
