using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>One row of the calendar screen - the same shape as the notes and task-list rows.</summary>
/// <param name="When">
/// Already in the reader's language, and written in their calendar's culture rather than the phone's -
/// reading an interface in Polish and being told "Monday, March 3" is only half a translation.
/// </param>
public sealed record CalendarEventRow(
    Guid LocalId, string Title, DateTimeOffset StartUtc, DateTimeOffset EndUtc, bool IsAllDay,
    bool HasUnsentChanges, OfflineEditRefusal Refusal, string When, string Status, bool IsCopy = false,
    string? Colour = null)
{
    public static CalendarEventRow From(
        LocalCalendarEvent calendarEvent, bool hasUnsentChanges, INetworkStatus networkStatus,
        Translations translations)
    {
        var details = calendarEvent.Details;
        var refusal = OfflineEditPolicy.Evaluate(calendarEvent, networkStatus);

        return new(
            calendarEvent.LocalId, details.Title, details.StartUtc, details.EndUtc, details.IsAllDay,
            hasUnsentChanges, refusal,
            Describe(details.StartUtc, details.EndUtc, details.IsAllDay, translations),
            OfflineEditExplanation.For(calendarEvent, refusal, hasUnsentChanges, translations),
            IsCopy: calendarEvent.CopyOfLocalId is not null, Colour: details.Color)
        {
            Day = details.StartUtc.LocalDateTime.ToString("ddd d", translations.DisplayCulture),
            Time = details.IsAllDay
                ? translations["all day"]
                : details.StartUtc.LocalDateTime.ToString("t", translations.DisplayCulture),
            Where = details.Location?.Address ?? string.Empty
        };
    }

    public bool HasStatus => Status.Length > 0;

    /// <summary>
    /// The day it falls on, in the reader's calendar - the head of the date column the list draws down
    /// its left-hand side. Short: the column is 58 points wide, and the year is either this one or
    /// said by the grid above the list.
    /// </summary>
    public string Day { get; init; } = string.Empty;

    /// <summary>
    /// What time it starts, under the day - or "all day" where there is no time to give, which is the
    /// same word <see cref="When"/> uses for the same event.
    /// </summary>
    public string Time { get; init; } = string.Empty;

    /// <summary>
    /// Where it is, as the reader wrote it. Empty for an event with no place, which is most of them -
    /// the row draws nothing rather than an empty second line.
    /// </summary>
    public string Where { get; init; } = string.Empty;

    private static string Describe(
        DateTimeOffset startUtc, DateTimeOffset endUtc, bool isAllDay, Translations translations)
        => isAllDay
            ? translations.Format("{0} · all day", startUtc.LocalDateTime.ToString("d", translations.DisplayCulture))
            : translations.Format(
                "{0} – {1}",
                startUtc.LocalDateTime.ToString("g", translations.DisplayCulture),
                endUtc.LocalDateTime.ToString("t", translations.DisplayCulture));
}
