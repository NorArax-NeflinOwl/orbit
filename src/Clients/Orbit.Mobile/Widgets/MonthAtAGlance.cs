using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Mobile.Widgets;

/// <summary>
/// One square of the month on the home screen.
/// </summary>
/// <param name="Number">
/// The day of the month, written out. Every square has one: the days before the first and after the
/// last belong to the months either side, and drawing them blank leaves a grid with holes in it.
/// </param>
/// <param name="IsInTheMonth">
/// False for those neighbours, which are drawn faintly - they are there to keep the weeks square, not
/// to be read.
/// </param>
/// <param name="HasSomething">
/// Whether anything wants this day: an appointment, or an entry due and not ticked off. What it is
/// stays off the home screen - see <see cref="MonthAtAGlance"/>.
/// </param>
public sealed record GlanceDay(string Number, bool IsInTheMonth, bool IsToday, bool HasSomething);

/// <summary>
/// The month as a grid, for the home screen widget that draws a calendar rather than a list.
///
/// Asked for on 2026-09-20 beside the one that was already there: <see cref="TodayAtAGlance"/> answers
/// "what is still ahead today", and there was nothing that answered "what does this month look like" -
/// which is the question somebody glancing at a home screen calendar is actually asking. So this says
/// nothing about *what* is on a day. A dot is the whole of it, deliberately:
///
/// - **Nothing is named.** A widget is on show to whoever is holding the phone, and on most Androids to
///   whoever can see the lock screen. Forty-two squares of titles would be the reader's month read out
///   loud to a room; a dot says as much as a glance needs and gives nothing away.
/// - **Nothing at all is shown to a phone nobody is signed in on**, for the reason
///   <see cref="TodayAtAGlance"/> gives: signing out leaves the local database where it is.
///
/// A dot means something *wants* that day - an appointment, or an entry due and not yet ticked off. A
/// day whose entries are all done carries none, which is the same rule the other widget follows: the
/// home screen is about what is left.
/// </summary>
/// <param name="Weekdays">
/// The seven initials across the top, starting on the day the reader's own language starts its week on -
/// Monday in Polish, Sunday in English.
/// </param>
/// <param name="Days">
/// Six weeks of seven, always, so the grid is the same height in a month that spills over a sixth week
/// as in one that does not. A widget that changes height between September and October is one the
/// launcher re-lays the home screen for.
/// </param>
/// <param name="Message">
/// What it says instead of a grid - a phone nobody is signed in on. Empty when there is a month to draw.
/// </param>
public sealed record MonthAtAGlance(
    string Month, IReadOnlyList<string> Weekdays, IReadOnlyList<GlanceDay> Days, string Message)
{
    /// <summary>Always six - see <see cref="Days"/>.</summary>
    public const int Weeks = 6;

    public const int DaysInAWeek = 7;

    /// <summary>What a phone nobody is signed in on shows. See the note on this type about why.</summary>
    public static MonthAtAGlance ForNobodySignedIn(Translations translations)
        => new(string.Empty, [], [], translations["Open Orbit to see your month"]);

    /// <summary>
    /// The month <paramref name="now"/> falls in. Worked out in the phone's own time zone throughout:
    /// a day is a local day, and an appointment at half past eleven at night belongs to the square the
    /// person looking at the phone would point at.
    /// </summary>
    public static MonthAtAGlance Of(
        IReadOnlyList<LocalTaskList> taskLists, IReadOnlyList<LocalCalendarEvent> events,
        DateTimeOffset now, Translations translations)
    {
        var culture = translations.DisplayCulture;
        var today = now.ToLocalTime().Date;
        var firstOfTheMonth = new DateTime(today.Year, today.Month, 1);
        var firstSquare = StartOfTheWeekOn(firstOfTheMonth, culture.DateTimeFormat.FirstDayOfWeek);
        var busy = WhatWantsADay(
            taskLists, events, firstSquare, firstSquare.AddDays(Weeks * DaysInAWeek), now, translations);

        return new MonthAtAGlance(
            firstOfTheMonth.ToString("MMMM yyyy", culture),
            [.. Initials(culture)],
            [
                .. Enumerable.Range(0, Weeks * DaysInAWeek)
                    .Select(square => firstSquare.AddDays(square))
                    .Select(day => new GlanceDay(
                        day.Day.ToString(culture),
                        day.Month == firstOfTheMonth.Month && day.Year == firstOfTheMonth.Year,
                        day == today,
                        busy.Contains(day)))
            ],
            string.Empty);
    }

    /// <summary>
    /// The grid a week at a time, which is how it is drawn - a row of seven views per week. Here rather
    /// than in the drawing so the shape of the month is settled in the half that can be tested.
    /// </summary>
    public IEnumerable<IReadOnlyList<GlanceDay>> InWeeks()
        => Enumerable.Range(0, Days.Count / DaysInAWeek)
            .Select(week => (IReadOnlyList<GlanceDay>)[.. Days.Skip(week * DaysInAWeek).Take(DaysInAWeek)]);

    /// <summary>
    /// Which days in the grid have anything on them. The whole visible range at once rather than a
    /// question per square: repeats are expanded by <see cref="CalendarOccurrences"/> over a span, and
    /// asking it forty-two times would expand the same weekly standup forty-two times.
    /// </summary>
    private static HashSet<DateTime> WhatWantsADay(
        IReadOnlyList<LocalTaskList> taskLists, IReadOnlyList<LocalCalendarEvent> events,
        DateTime from, DateTime to, DateTimeOffset now, Translations translations)
    {
        var occurrences = CalendarOccurrences.Between(
            events, new DateTimeOffset(from, now.Offset), new DateTimeOffset(to, now.Offset));

        // The same rule the other widget applies, and for the same reason: a private or sealed list is
        // not shown on a home screen at all, not even as a dot. See MonthAtAGlance's own note.
        var readable = taskLists.Where(taskList => !taskList.IsPrivate && !taskList.IsSealed).ToList();

        return
        [
            .. occurrences.Select(occurrence => occurrence.Details.StartUtc.ToLocalTime().Date),
            // The translations are only what it writes its labels in, which nothing here reads - a dot
            // says nothing. They are passed rather than stubbed so the one code path stays the one path.
            .. CalendarDeadline.From(readable, occurrences, translations)
                .Where(deadline => !deadline.IsCompleted)
                .Select(deadline => deadline.DueLocalDate)
        ];
    }

    /// <summary>
    /// The seven initials, from the day this language starts its week on. Taken from the culture rather
    /// than written out: a Polish week starts on Monday and an English one on Sunday, and a grid whose
    /// columns disagree with the reader's own calendar is worse than no grid.
    /// </summary>
    private static IEnumerable<string> Initials(System.Globalization.CultureInfo culture)
    {
        var first = (int)culture.DateTimeFormat.FirstDayOfWeek;
        var names = culture.DateTimeFormat.ShortestDayNames;

        return Enumerable.Range(0, DaysInAWeek).Select(offset => names[(first + offset) % DaysInAWeek]);
    }

    private static DateTime StartOfTheWeekOn(DateTime day, DayOfWeek firstDayOfWeek)
        => day.AddDays(-(((int)day.DayOfWeek - (int)firstDayOfWeek + DaysInAWeek) % DaysInAWeek));
}
