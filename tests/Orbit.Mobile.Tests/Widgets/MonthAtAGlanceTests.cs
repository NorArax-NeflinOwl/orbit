using Orbit.Contracts.Calendar;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Tests.TestDoubles;
using Orbit.Mobile.Widgets;
using Xunit;

namespace Orbit.Mobile.Tests.Widgets;

/// <summary>
/// The month the second home screen widget draws - asked for on 2026-09-20, beside the one that lists
/// what is left today.
///
/// Pinned as tightly as <see cref="TodayAtAGlanceTests"/> and for the same reason: nothing about a
/// widget is observable from inside the app. A grid whose weeks start on the wrong day, or which marks
/// somebody's private list, is not something anybody reports - they stop looking at it.
/// </summary>
public sealed class MonthAtAGlanceTests
{
    /// <summary>A Tuesday, in a month that starts on one - see the grid tests below.</summary>
    private static readonly DateTimeOffset Morning = Local(new DateTime(2026, 9, 1, 8, 0, 0));

    [Fact]
    public void The_month_is_named_over_the_grid()
        => Assert.Equal("September 2026", MonthAtAGlance.Of([], [], Morning, English()).Month);

    /// <summary>
    /// Six weeks of seven, whatever the month. A widget that is five rows tall in one month and six in
    /// the next is one the launcher re-lays the home screen around.
    /// </summary>
    [Fact]
    public void It_is_always_six_weeks_of_seven()
    {
        var month = MonthAtAGlance.Of([], [], Morning, English());

        Assert.Equal(42, month.Days.Count);
        Assert.Equal(6, month.InWeeks().Count());
        Assert.All(month.InWeeks(), week => Assert.Equal(7, week.Count));
    }

    /// <summary>
    /// The week starts where the reader's own language starts it. English begins on Sunday and Polish
    /// on Monday, and a grid whose columns disagree with the reader's own calendar is worse than none.
    /// </summary>
    [Fact]
    public void An_english_week_starts_on_sunday_and_a_polish_one_on_monday()
    {
        var english = MonthAtAGlance.Of([], [], Morning, English());
        var polish = MonthAtAGlance.Of([], [], Morning, Polish());

        // 1 September 2026 is a Tuesday. English puts it in the third square of the first week
        // (Sunday, Monday, Tuesday); Polish in the second (Monday, Tuesday).
        Assert.Equal("1", english.Days[2].Number);
        Assert.True(english.Days[2].IsInTheMonth);
        Assert.Equal("1", polish.Days[1].Number);
        Assert.True(polish.Days[1].IsInTheMonth);
        Assert.Equal(7, english.Weekdays.Count);
    }

    /// <summary>
    /// The squares either side belong to the months next door. Drawn, and marked as not this month's:
    /// leaving them blank would leave the grid with holes in it.
    /// </summary>
    [Fact]
    public void The_days_either_side_are_there_but_not_of_this_month()
    {
        var month = MonthAtAGlance.Of([], [], Morning, English());

        Assert.Equal(["30", "31"], month.Days.Take(2).Select(day => day.Number));
        Assert.All(month.Days.Take(2), day => Assert.False(day.IsInTheMonth));
        Assert.Equal(30, month.Days.Count(day => day.IsInTheMonth));
    }

    [Fact]
    public void Today_is_the_one_square_marked_as_today()
    {
        var month = MonthAtAGlance.Of([], [], Morning, English());

        var today = Assert.Single(month.Days, day => day.IsToday);
        Assert.Equal("1", today.Number);
        Assert.True(today.IsInTheMonth);
    }

    [Fact]
    public void A_day_with_an_appointment_on_it_carries_a_dot()
    {
        var month = MonthAtAGlance.Of([], [Appointment("Dentist", At(9, 30))], Morning, English());

        var dotted = Assert.Single(month.Days, day => day.HasSomething);
        Assert.Equal("1", dotted.Number);
    }

    /// <summary>
    /// A weekly standup is stored once, on the week it began, and a grid reading the stored rows would
    /// mark that one day and no other - see CalendarOccurrences.
    /// </summary>
    [Fact]
    public void A_repeat_marks_every_day_it_falls_on()
    {
        var weekly = Appointment("Stand-up", At(9, 0));
        weekly.Details = weekly.Details with
        {
            Recurrence = new RecurrenceDto("Weekly", 1, null)
        };

        var month = MonthAtAGlance.Of([], [weekly], Morning, English());

        // Tuesdays from the 1st: the 1st, 8th, 15th, 22nd and 29th, plus the two Tuesdays of the
        // neighbouring months the grid shows.
        Assert.Contains(month.Days, day => day is { Number: "8", HasSomething: true, IsInTheMonth: true });
        Assert.Contains(month.Days, day => day is { Number: "29", HasSomething: true, IsInTheMonth: true });
        Assert.DoesNotContain(month.Days, day => day is { Number: "9", HasSomething: true });
    }

    /// <summary>
    /// A dot means something still wants that day. An entry that has been ticked off does not, which is
    /// the rule the other widget follows too - the home screen is about what is left.
    /// </summary>
    [Fact]
    public void An_entry_due_but_not_done_marks_its_day_and_a_finished_one_does_not()
    {
        var due = ListCalled("Shopping", DueEntry("Milk", At(17, 0)));
        var done = ListCalled("Errands", DueEntry("Post the parcel", At(17, 0)) with { IsCompleted = true });

        Assert.Single(MonthAtAGlance.Of([due], [], Morning, English()).Days, day => day.HasSomething);
        Assert.DoesNotContain(
            MonthAtAGlance.Of([done], [], Morning, English()).Days, day => day.HasSomething);
    }

    /// <summary>
    /// Not even as a dot. A widget is on show to whoever is holding the phone, and the gate that guards
    /// private items inside the app has no equivalent out here - see TodayAtAGlance for the same rule.
    /// </summary>
    [Fact]
    public void A_private_or_sealed_list_marks_nothing()
    {
        var hidden = ListCalled("Presents", DueEntry("Order it", At(17, 0)));
        hidden.IsPrivate = true;
        var locked = ListCalled("Also presents", DueEntry("Wrap it", At(17, 0)));
        locked.IsSealed = true;

        Assert.DoesNotContain(
            MonthAtAGlance.Of([hidden, locked], [], Morning, English()).Days, day => day.HasSomething);
    }

    [Fact]
    public void A_phone_nobody_is_signed_in_on_shows_no_month_at_all()
    {
        var month = MonthAtAGlance.ForNobodySignedIn(English());

        Assert.Empty(month.Days);
        Assert.Empty(month.Month);
        Assert.Equal("Open Orbit to see your month", month.Message);
    }

    [Fact]
    public void It_is_read_in_the_language_the_reader_chose()
        => Assert.Contains(
            "wrzesień", MonthAtAGlance.Of([], [], Morning, Polish()).Month, StringComparison.OrdinalIgnoreCase);

    private static Translations English() => new(new InMemoryLanguageStore());

    private static Translations Polish()
    {
        var translations = English();
        translations.SetLanguage(Orbit.Localization.AppLanguage.Polish);
        return translations;
    }

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

    private static LocalTaskList ListCalled(string title, params TaskItemDto[] items)
        => new() { LocalId = Guid.NewGuid(), ServerId = Guid.NewGuid(), Title = title, Items = items };

    private static TaskItemDto DueEntry(string description, DateTimeOffset due)
        => new(
            Guid.NewGuid(), description, due.ToUniversalTime(), false, null, "None", false, "None",
            new TimeOnly(9, 0));
}
