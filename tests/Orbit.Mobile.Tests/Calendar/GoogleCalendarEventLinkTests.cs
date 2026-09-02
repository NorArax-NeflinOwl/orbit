using System.Globalization;
using Orbit.Contracts.Calendar;
using Orbit.Mobile.Google;
using Xunit;

namespace Orbit.Mobile.Tests.Calendar;

/// <summary>
/// The URLs this phone hands to Google. Its own tests rather than shared ones because the builder is
/// its own twin of Orbit.Web's (see GoogleMapsLink for why), and the tests on both sides are the only
/// thing that keeps the two saying the same thing - which they had stopped doing.
/// </summary>
public sealed class GoogleCalendarEventLinkTests
{
    private static readonly DateTimeOffset TenAm = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Orbit's end date is the last day the event covers; Google's is the first day it does not. A trip
    /// from the 1st to the 4th is four days in Orbit and has to be four in the link. This one used to
    /// add the day only when the end was already past the start, which made every multi-day event a day
    /// short - the same bug the browser had, fixed there in August.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 4)]
    [InlineData(14, 16)]
    public void An_all_day_event_covers_the_days_it_says_it_does(int firstDay, int lastDay)
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Trip", LocalMidnightOn(firstDay), LocalMidnightOn(lastDay)) { IsAllDay = true });

        var range = link.Split("dates=")[1].Split('&')[0].Split('/');
        var googleEnd = int.Parse(range[1][6..], CultureInfo.InvariantCulture);
        Assert.Equal(lastDay - firstDay + 1, googleEnd - firstDay);
    }

    [Fact]
    public void Only_the_first_line_of_a_name_can_be_the_title()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Dentist\nBring the x-rays", TenAm, TenAm.AddHours(1)));

        Assert.Contains("text=Dentist&", link);
        Assert.Contains("Bring+the+x-rays", link);
    }

    [Fact]
    public void An_appointment_a_list_raised_says_which_list()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Dentist", TenAm, TenAm.AddHours(1)) { TaskListTitle = "Health" });

        Assert.Contains("text=Health+-+Dentist", link);
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

    /// <summary>Google's form takes no reminders, so what Orbit would have said travels as words.</summary>
    [Fact]
    public void Reminders_are_written_into_the_description()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Dentist", TenAm, TenAm.AddHours(1))
            {
                ReminderMinutesBeforeStart = [30],
                NotifyAtStart = true
            });

        Assert.Contains("30+min+before", link);
        Assert.Contains("at+the+start", link);
    }

    [Fact]
    public void An_event_with_nothing_to_add_says_nothing()
    {
        var link = GoogleCalendarEventLink.ForEvent(new GoogleCalendarEvent("Dentist", TenAm, TenAm.AddHours(1)));

        Assert.DoesNotContain("details=", link);
        Assert.DoesNotContain("add=", link, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recur", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_repeating_event_carries_its_rule()
    {
        var link = GoogleCalendarEventLink.ForEvent(
            new GoogleCalendarEvent("Standup", TenAm, TenAm.AddMinutes(15))
            {
                Recurrence = new RecurrenceDto("Daily", 1, null)
            });

        Assert.Contains("recur=RRULE%3aFREQ%3dDAILY", link, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An all-day event is stored as the instant local midnight began, so these build theirs the same
    /// way - which is what a real one looks like and the only shape the day-short bug shows up in.
    /// </summary>
    private static DateTimeOffset LocalMidnightOn(int day)
        => new(new DateTime(2026, 9, day, 0, 0, 0, DateTimeKind.Local));
}
