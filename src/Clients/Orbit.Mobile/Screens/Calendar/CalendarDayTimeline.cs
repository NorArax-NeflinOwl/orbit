using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// One thing on a day, at the height it belongs and beside whatever it overlaps.
/// </summary>
/// <param name="StartMinute">Minutes from midnight, clamped to the day - an event may begin before it.</param>
/// <param name="Column">
/// Which of <paramref name="ColumnCount"/> side-by-side lanes this sits in. Two things at the same time
/// are the whole reason a day is drawn rather than listed: a list puts one under the other and says
/// nothing about the clash.
/// </param>
public sealed record DayBlock(
    Guid LocalId, string Title, string When, int StartMinute, int EndMinute, int Column, int ColumnCount)
{
    /// <summary>Never nothing: a zero-length event would be a line too thin to read or tap.</summary>
    public int Minutes => Math.Max(EndMinute - StartMinute, MinimumMinutes);

    /// <summary>Roughly a finger, at the height a whole day is drawn in.</summary>
    public const int MinimumMinutes = 30;
}

/// <summary>
/// A day laid out by the hour - Orbit.Web's day view, which the phone did not have. Choosing a day
/// narrowed a list, and a list answers "what is on today" by losing the three things the question is
/// usually about: how long each thing takes, what clashes with what, and where the gaps are.
///
/// The placement is Orbit.Web's, worked out again rather than shared: its CalendarGridBuilder is built
/// on the wire DTOs and lives in a client the phone cannot reference. What is shared is the arithmetic
/// underneath - see CalendarOccurrences for the repeats, which are expanded before this is asked.
/// </summary>
public static class CalendarDayTimeline
{
    public const int MinutesInADay = 24 * 60;

    /// <summary>
    /// The stretch of the clock worth drawing: the hour the day's first thing starts in, to the hour
    /// its last thing ends in. All twenty-four would be mostly empty - a day with one meeting at nine
    /// would open on midnight and ask the reader to scroll past eight hours of nothing to find it.
    /// </summary>
    /// <returns>Inclusive at both ends, so an hour with something in it is always drawn whole.</returns>
    public static (int FirstHour, int LastHour) HoursWorthDrawing(IReadOnlyList<DayBlock> blocks)
    {
        if (blocks.Count == 0)
        {
            return (0, 0);
        }

        var first = blocks.Min(block => block.StartMinute) / 60;
        // The hour the last thing ends in, held inside the day: something running to midnight belongs
        // to the last hour there is rather than to the first hour of a day nobody asked for.
        var last = Math.Min(blocks.Max(block => block.StartMinute + block.Minutes) / 60, 23);
        return (first, Math.Max(first, last));
    }

    /// <summary>
    /// Everything happening on <paramref name="day"/> that has a time, placed. All-day events are not
    /// here: they belong to the whole day rather than an hour of it - see <see cref="AllDayOn"/>.
    /// </summary>
    public static IReadOnlyList<DayBlock> Build(
        DateTime day, IReadOnlyList<LocalCalendarEvent> events, Translations translations)
        => PlaceSideBySide(
            [.. events
                .Where(calendarEvent => !calendarEvent.Details.IsAllDay)
                .Where(calendarEvent => IsOn(calendarEvent, day))
                .Select(calendarEvent => Slot(calendarEvent, day, translations))
                .OrderBy(block => block.StartMinute)
                .ThenBy(block => block.EndMinute)]);

    /// <summary>What has no hour to be drawn at, which the day shows in a row of its own above the clock.</summary>
    public static IReadOnlyList<DayBlock> AllDayOn(
        DateTime day, IReadOnlyList<LocalCalendarEvent> events, Translations translations)
        => [.. events
            .Where(calendarEvent => calendarEvent.Details.IsAllDay)
            .Where(calendarEvent => IsOn(calendarEvent, day))
            .OrderBy(calendarEvent => calendarEvent.Details.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(calendarEvent => new DayBlock(
                calendarEvent.LocalId, calendarEvent.Details.Title, string.Empty, 0, 0, 0, 1))];

    /// <summary>
    /// Whether any part of the event falls on this day - one that began yesterday evening and runs into
    /// this morning is on both, and drawing it only on the day it started would lose the morning.
    /// </summary>
    private static bool IsOn(LocalCalendarEvent calendarEvent, DateTime day)
    {
        var start = calendarEvent.Details.StartUtc.ToLocalTime().DateTime;
        var end = calendarEvent.Details.EndUtc.ToLocalTime().DateTime;
        return start.Date <= day.Date && end >= day.Date;
    }

    /// <summary>
    /// Where it sits on this day's clock, clamped to it at both ends: what runs past midnight is drawn
    /// to the edge rather than off it.
    /// </summary>
    private static DayBlock Slot(LocalCalendarEvent calendarEvent, DateTime day, Translations translations)
    {
        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1);
        var start = calendarEvent.Details.StartUtc.ToLocalTime().DateTime;
        var end = calendarEvent.Details.EndUtc.ToLocalTime().DateTime;

        var startMinute = (int)(Later(start, dayStart) - dayStart).TotalMinutes;
        var endMinute = Math.Max(startMinute, (int)(Earlier(end, dayEnd) - dayStart).TotalMinutes);

        return new DayBlock(
            calendarEvent.LocalId, calendarEvent.Details.Title,
            Describe(start, end, translations), startMinute, endMinute, Column: 0, ColumnCount: 1);
    }

    private static DateTime Later(DateTime one, DateTime other) => one < other ? other : one;

    private static DateTime Earlier(DateTime one, DateTime other) => one > other ? other : one;

    private static string Describe(DateTime start, DateTime end, Translations translations)
        => $"{start.ToString("t", translations.DisplayCulture)} – {end.ToString("t", translations.DisplayCulture)}";

    /// <summary>
    /// Splits the day into runs of things that touch, and gives each run as many lanes as its busiest
    /// moment needs - the same placement Orbit.Web's grid makes. A lane is reused as soon as whatever
    /// held it has finished, so two meetings an hour apart share one and the day stays readable.
    /// </summary>
    private static IReadOnlyList<DayBlock> PlaceSideBySide(IReadOnlyList<DayBlock> blocksByStart)
    {
        var placed = new List<DayBlock>();
        var run = new List<DayBlock>();
        var runEndMinute = 0;

        foreach (var block in blocksByStart)
        {
            if (run.Count > 0 && block.StartMinute >= runEndMinute)
            {
                placed.AddRange(PlaceWithinRun(run));
                run.Clear();
            }

            run.Add(block);
            runEndMinute = Math.Max(runEndMinute, block.EndMinute);
        }

        placed.AddRange(PlaceWithinRun(run));
        return placed;
    }

    private static IEnumerable<DayBlock> PlaceWithinRun(IReadOnlyList<DayBlock> run)
    {
        var laneEndMinutes = new List<int>();
        var laneOf = new int[run.Count];

        for (var position = 0; position < run.Count; position++)
        {
            var block = run[position];
            var lane = laneEndMinutes.FindIndex(endMinute => endMinute <= block.StartMinute);
            if (lane < 0)
            {
                lane = laneEndMinutes.Count;
                laneEndMinutes.Add(block.EndMinute);
            }
            else
            {
                laneEndMinutes[lane] = block.EndMinute;
            }

            laneOf[position] = lane;
        }

        return run.Select((block, position) => block with { Column = laneOf[position], ColumnCount = laneEndMinutes.Count });
    }
}
