using Orbit.Contracts.Calendar;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Tests.TestDoubles;
using Orbit.Mobile.Widgets;
using Xunit;

namespace Orbit.Mobile.Tests.Widgets;

/// <summary>
/// What the home screen widget puts in its four lines.
///
/// Worth pinning tightly because nothing about a widget is observable from inside the app: it is drawn
/// by the launcher, in another process, from a snapshot taken up to half an hour earlier. There is no
/// screen to open and check, and a widget showing the wrong day, somebody's private list, or an empty
/// box is not a failure anybody reports - they just stop looking at it.
/// </summary>
public sealed class TodayAtAGlanceTests
{
    private static readonly DateTimeOffset Morning = Local(new DateTime(2026, 9, 1, 8, 0, 0));

    [Fact]
    public void The_day_is_named_before_what_is_in_it()
    {
        var glance = TodayAtAGlance.Of([], [], Morning, English());

        // The same opening Orbit.Web's today strip has: what "today" means, before what is on it.
        Assert.Equal("Tuesday, 1 September", glance.Date);
    }

    [Fact]
    public void An_appointment_today_is_on_it_at_the_time_it_starts()
    {
        var glance = TodayAtAGlance.Of([], [Appointment("Dentist", At(9, 30))], Morning, English());

        var line = Assert.Single(glance.Lines);
        Assert.Equal("Dentist", line.What);
        // Compared without its spacing: .NET writes a narrow no-break space before AM, and what this
        // test is about is that the time is the one the appointment starts at.
        Assert.Equal("9:30AM", line.When.Replace('\u202f', ' ').Replace(" ", string.Empty));
        Assert.Equal("/calendar", line.Url);
    }

    /// <summary>
    /// The widget answers "what is still ahead", not "what happened". A meeting that ended before
    /// breakfast is four lines' worth of nothing.
    /// </summary>
    [Fact]
    public void An_appointment_that_has_already_finished_is_not_on_it()
    {
        var glance = TodayAtAGlance.Of([], [Appointment("Stand-up", At(7, 0))], Morning, English());

        Assert.Empty(glance.Lines);
        Assert.Equal("Nothing left today", glance.Message);
    }

    /// <summary>An all-day event has no time to be past, and belongs at the top of the day it is on.</summary>
    [Fact]
    public void An_all_day_event_is_first_and_says_so()
    {
        var glance = TodayAtAGlance.Of(
            [], [Appointment("Lunch", At(13, 0)), AllDay("Krystyna's birthday")], Morning, English());

        Assert.Equal("Krystyna's birthday", glance.Lines[0].What);
        Assert.Equal("All day", glance.Lines[0].When);
    }

    /// <summary>Being in the middle of something is not the same as having finished it.</summary>
    [Fact]
    public void An_appointment_already_under_way_stays_on_it()
    {
        var glance = TodayAtAGlance.Of([], [Appointment("Workshop", At(7, 30))], Morning, English());

        Assert.Equal("Workshop", Assert.Single(glance.Lines).What);
    }

    [Fact]
    public void Tomorrow_is_not_today()
    {
        var glance = TodayAtAGlance.Of(
            [], [Appointment("Tomorrow's meeting", Local(new DateTime(2026, 9, 2, 9, 0, 0)))], Morning, English());

        Assert.Empty(glance.Lines);
    }

    /// <summary>
    /// A repeat is stored once, on the week it began. Read straight from the stored rows, a weekly
    /// stand-up would appear on the home screen that first week and never again.
    /// </summary>
    [Fact]
    public void A_weekly_repeat_is_on_the_day_it_falls_rather_than_the_day_it_was_made()
    {
        var weekly = Appointment("Stand-up", Local(new DateTime(2026, 8, 4, 9, 30, 0)));
        weekly.Details = weekly.Details with { Recurrence = new RecurrenceDto("Weekly", 1, null) };

        var glance = TodayAtAGlance.Of([], [weekly], Morning, English());

        Assert.Equal("Stand-up", Assert.Single(glance.Lines).What);
    }

    [Fact]
    public void Something_due_today_is_on_it_with_the_list_it_is_on()
    {
        var taskLists = new[] { ListCalled("Shopping", DueEntry("Milk", At(17, 0))) };

        var glance = TodayAtAGlance.Of(taskLists, [], Morning, English());

        var line = Assert.Single(glance.Lines);
        Assert.Equal("Shopping: Milk", line.What);
        Assert.Equal($"/tasks/{taskLists[0].ServerId}", line.Url);
    }

    /// <summary>Unlike an appointment: an errand whose time has passed is exactly what to be reminded of.</summary>
    [Fact]
    public void Something_that_was_due_this_morning_stays_on_it()
    {
        var glance = TodayAtAGlance.Of([ListCalled("Jobs", DueEntry("Post the letter", At(7, 0)))], [], Morning, English());

        Assert.Single(glance.Lines);
    }

    [Fact]
    public void Something_already_done_is_not_on_it()
    {
        var done = DueEntry("Milk", At(17, 0)) with { IsCompleted = true };

        var glance = TodayAtAGlance.Of([ListCalled("Shopping", done)], [], Morning, English());

        Assert.Empty(glance.Lines);
    }

    /// <summary>
    /// The rule this whole type exists to keep. A widget is on show to whoever is holding the phone,
    /// and on most Androids to whoever can see the lock screen - there is no gate out there to put a
    /// private list behind.
    /// </summary>
    [Fact]
    public void A_private_list_is_never_named_on_the_home_screen()
    {
        var privateList = ListCalled("Doctor", DueEntry("Prescription", At(17, 0)));
        privateList.IsPrivate = true;

        var glance = TodayAtAGlance.Of([privateList], [], Morning, English());

        Assert.Empty(glance.Lines);
        Assert.DoesNotContain("Prescription", glance.Message);
    }

    /// <summary>A sealed list's title is ciphertext, and ciphertext on a home screen is worse than nothing.</summary>
    [Fact]
    public void A_list_this_phone_cannot_open_is_left_off()
    {
        var sealedList = ListCalled("8mK2vQ==", DueEntry("9xR1==", At(17, 0)));
        sealedList.IsSealed = true;

        Assert.Empty(TodayAtAGlance.Of([sealedList], [], Morning, English()).Lines);
    }

    [Fact]
    public void Everything_is_in_the_order_it_happens()
    {
        var glance = TodayAtAGlance.Of(
            [ListCalled("Jobs", DueEntry("Post the letter", At(12, 0)))],
            [Appointment("Dentist", At(9, 30)), Appointment("Lunch", At(13, 0))],
            Morning, English());

        Assert.Equal(["Dentist", "Jobs: Post the letter", "Lunch"], glance.Lines.Select(line => line.What));
    }

    /// <summary>Four fit. The fifth thing is not dropped in silence - the widget says how many are behind it.</summary>
    [Fact]
    public void What_does_not_fit_is_counted_rather_than_dropped()
    {
        var appointments = Enumerable.Range(9, 6)
            .Select(hour => Appointment($"Meeting at {hour}", At(hour, 0)))
            .ToArray();

        var glance = TodayAtAGlance.Of([], appointments, Morning, English());

        Assert.Equal(TodayAtAGlance.MostLines, glance.Lines.Count);
        Assert.Equal("2 more", glance.More);
        Assert.Empty(glance.Message);
    }

    [Fact]
    public void A_day_that_fits_says_nothing_about_more()
    {
        var glance = TodayAtAGlance.Of([], [Appointment("Dentist", At(9, 30))], Morning, English());

        Assert.Empty(glance.More);
    }

    /// <summary>
    /// Signing out leaves the local database where it is, so a widget that read it would go on showing
    /// the previous account's day to whoever picks the phone up next.
    /// </summary>
    [Fact]
    public void A_phone_nobody_is_signed_in_on_shows_no_day_at_all()
    {
        var glance = TodayAtAGlance.ForNobodySignedIn(English());

        Assert.Empty(glance.Lines);
        Assert.Empty(glance.Date);
        Assert.Equal("Open Orbit to see your day", glance.Message);
    }

    /// <summary>
    /// A list made on this phone and not yet sent has no server id, and the paths a tap travels through
    /// are the server's - see NotificationDestination. It opens Orbit rather than nowhere.
    /// </summary>
    [Fact]
    public void An_entry_on_a_list_that_has_never_been_sent_still_opens_the_app()
    {
        var neverSent = ListCalled("Shopping", DueEntry("Milk", At(17, 0)));
        neverSent.ServerId = null;

        Assert.Equal(string.Empty, Assert.Single(TodayAtAGlance.Of([neverSent], [], Morning, English()).Lines).Url);
    }

    [Fact]
    public void It_is_read_in_the_language_the_reader_chose()
    {
        var polish = English();
        polish.SetLanguage(Orbit.Localization.AppLanguage.Polish);

        var glance = TodayAtAGlance.Of([], [], Morning, polish);

        Assert.Equal("Nic już na dziś", glance.Message);
        Assert.Contains("września", glance.Date, StringComparison.OrdinalIgnoreCase);
    }

    private static Translations English() => new(new InMemoryLanguageStore());

    private static DateTimeOffset At(int hour, int minute)
        => Local(new DateTime(2026, 9, 1, hour, minute, 0));

    private static DateTimeOffset Local(DateTime moment)
        => new(moment, TimeZoneInfo.Local.GetUtcOffset(moment));

    private static LocalCalendarEvent Appointment(string title, DateTimeOffset start)
        => new()
        {
            LocalId = Guid.NewGuid(),
            ServerId = Guid.NewGuid(),
            Details = new CalendarEventDetailsDto(
                title, null, null, null, start.ToUniversalTime(), start.AddHours(1).ToUniversalTime(),
                false, null, [], [], ReminderNotificationChannel: "None")
        };

    private static LocalCalendarEvent AllDay(string title)
    {
        var appointment = Appointment(title, At(0, 0));
        appointment.Details = appointment.Details with { IsAllDay = true };
        return appointment;
    }

    private static LocalTaskList ListCalled(string title, params TaskItemDto[] items)
        => new() { LocalId = Guid.NewGuid(), ServerId = Guid.NewGuid(), Title = title, Items = items };

    private static TaskItemDto DueEntry(string description, DateTimeOffset due)
        => new(
            Guid.NewGuid(), description, due.ToUniversalTime(), false, null, "None", false, "None",
            new TimeOnly(9, 0));
}
