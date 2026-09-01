using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Mobile.Widgets;

/// <summary>
/// One line of the home screen widget.
/// </summary>
/// <param name="When">The time it happens, already written in the reader's language.</param>
/// <param name="Url">
/// Where tapping it leads, in the same paths a notification uses - see NotificationDestination. Empty
/// for a line with nowhere of its own to go, which opens Orbit where it would have opened anyway.
/// </param>
public sealed record GlanceLine(string When, string What, string Url);

/// <summary>
/// What the home screen widget shows: the day, and the few things left in it.
///
/// A widget is read at arm's length and in a glance, so this is not the dashboard in miniature. It
/// answers one question - what is still ahead today - and everything that does not help answer it is
/// left out: what was already done, what has already finished, and anything belonging to another day.
///
/// Two rules are here for the home screen specifically rather than copied from any screen:
///
/// - <b>Nothing private is ever named.</b> A widget is on show to whoever is holding the phone, and on
///   most Androids to whoever can see the lock screen, with no unlocking in between. The gate that
///   guards private items inside the app has no equivalent out here, so they are dropped rather than
///   hidden behind it.
/// - <b>Nothing at all is shown to a phone nobody is signed in on.</b> Signing out leaves the local
///   database where it is - see AuthenticationClient.SignOutAsync - so a widget reading it would go on
///   showing the previous account's day to the next person holding the phone.
/// </summary>
/// <param name="Message">
/// What the widget says when it has no lines: a phone with nothing left today, or one nobody is signed
/// in on. Empty when there are lines. A widget showing an empty box reads as broken.
/// </param>
/// <param name="More">
/// How many more there were than fit, already written out - "3 more", or empty when everything is on it.
/// A count with nothing said about it is a number on a home screen nobody can read.
/// </param>
public sealed record TodayAtAGlance(string Date, IReadOnlyList<GlanceLine> Lines, string Message, string More = "")
{
    /// <summary>
    /// How many lines fit. Four, because the smallest cell a widget can be placed in is about that tall
    /// and the alternative - a scrolling list - is a collection widget: a second process, a factory per
    /// row, and a service to host it, for a view somebody looks at for a second and a half.
    /// </summary>
    public const int MostLines = 4;

    /// <summary>What a phone nobody is signed in on shows. See the note on this type about why.</summary>
    public static TodayAtAGlance ForNobodySignedIn(Translations translations)
        => new(string.Empty, [], translations["Open Orbit to see your day"]);

    /// <summary>
    /// The day as it stands at <paramref name="now"/>. Everything is worked out in the phone's own time
    /// zone: "today" is a local day, and an event at half past eleven at night is today's for the person
    /// looking at the phone whatever the stored instant says.
    /// </summary>
    public static TodayAtAGlance Of(
        IReadOnlyList<LocalTaskList> taskLists, IReadOnlyList<LocalCalendarEvent> events,
        DateTimeOffset now, Translations translations)
    {
        var today = now.ToLocalTime().Date;
        var lines = WhatIsLeft(taskLists, events, now, today, translations);

        return new TodayAtAGlance(
            today.ToString("dddd, d MMMM", translations.DisplayCulture),
            [.. lines.Take(MostLines)],
            lines.Count == 0 ? translations["Nothing left today"] : string.Empty,
            lines.Count > MostLines ? translations.Format("{0} more", lines.Count - MostLines) : string.Empty);
    }

    private static IReadOnlyList<GlanceLine> WhatIsLeft(
        IReadOnlyList<LocalTaskList> taskLists, IReadOnlyList<LocalCalendarEvent> events,
        DateTimeOffset now, DateTime today, Translations translations)
    {
        // Repeats expanded first: a weekly standup is stored once, on the week it began, and a widget
        // reading the stored rows would show it on that day and never again - see CalendarOccurrences.
        var occurrences = CalendarOccurrences.Between(
            events, new DateTimeOffset(today, now.Offset), new DateTimeOffset(today.AddDays(1), now.Offset));
        var readable = taskLists.Where(taskList => !taskList.IsPrivate && !taskList.IsSealed).ToList();

        return
        [
            .. Appointments(occurrences, now, today, translations)
                .Concat(Deadlines(readable, occurrences, today, translations))
                .OrderBy(line => line.At)
                .Select(line => line.Line)
        ];
    }

    /// <summary>
    /// Today's appointments that have not finished yet - one already under way included, since that is
    /// still what the reader is in the middle of. One that ended an hour ago is not something to be
    /// prompted about, and the widget has four lines to spend.
    /// </summary>
    private static IEnumerable<(DateTimeOffset At, GlanceLine Line)> Appointments(
        IReadOnlyList<LocalCalendarEvent> occurrences, DateTimeOffset now, DateTime today,
        Translations translations)
        => occurrences
            .Where(occurrence => occurrence.Details.StartUtc.ToLocalTime().Date == today)
            .Where(occurrence => occurrence.Details.IsAllDay || occurrence.Details.EndUtc > now)
            .Select(occurrence => (
                // An all-day event is sorted to the top of its day rather than to whatever instant it
                // happens to be stored at, which is where the reader looks for it.
                At: occurrence.Details.IsAllDay
                    ? new DateTimeOffset(today, now.Offset)
                    : occurrence.Details.StartUtc,
                Line: new GlanceLine(
                    occurrence.Details.IsAllDay
                        ? translations["All day"]
                        : occurrence.Details.StartUtc.ToLocalTime().ToString("t", translations.DisplayCulture),
                    occurrence.Details.Title,
                    // The calendar rather than the event: there is a screen for one event, but the
                    // notification paths a tap travels through name no event - see NotificationDestination.
                    "/calendar")));

    /// <summary>
    /// What falls due today and is not done. Unlike an appointment, one whose time has passed stays on:
    /// that is exactly what somebody wants their home screen to tell them.
    /// </summary>
    private static IEnumerable<(DateTimeOffset At, GlanceLine Line)> Deadlines(
        IReadOnlyList<LocalTaskList> taskLists, IReadOnlyList<LocalCalendarEvent> occurrences, DateTime today,
        Translations translations)
    {
        var listsByLocalId = taskLists.ToDictionary(taskList => taskList.LocalId);

        return CalendarDeadline.From(taskLists, occurrences, translations)
            .Where(deadline => deadline.DueLocalDate == today && !deadline.IsCompleted)
            .Select(deadline => (
                At: DueAt(listsByLocalId, deadline, today),
                Line: new GlanceLine(
                    DueAt(listsByLocalId, deadline, today).ToString("t", translations.DisplayCulture),
                    deadline.Label,
                    UrlOf(listsByLocalId, deadline))));
    }

    private static DateTimeOffset DueAt(
        IReadOnlyDictionary<Guid, LocalTaskList> taskLists, CalendarDeadline deadline, DateTime today)
        => ItemOf(taskLists, deadline)?.DueDateUtc?.ToLocalTime() ?? new DateTimeOffset(today, TimeSpan.Zero);

    /// <summary>
    /// The list the entry sits on, which is where it is ticked off. A list this phone made and has not
    /// managed to send yet has no server id to name, and the paths a tap travels through are the
    /// server's - so it opens Orbit without saying where, rather than somewhere that cannot be found.
    /// </summary>
    private static string UrlOf(IReadOnlyDictionary<Guid, LocalTaskList> taskLists, CalendarDeadline deadline)
        => taskLists.TryGetValue(deadline.TaskListLocalId, out var taskList) && taskList.ServerId is { } serverId
            ? $"/tasks/{serverId}"
            : string.Empty;

    private static Contracts.Tasks.TaskItemDto? ItemOf(
        IReadOnlyDictionary<Guid, LocalTaskList> taskLists, CalendarDeadline deadline)
        => taskLists.TryGetValue(deadline.TaskListLocalId, out var taskList)
            ? taskList.Items.FirstOrDefault(item => item.Id == deadline.ItemId)
            : null;
}
