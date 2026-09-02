namespace Orbit.Mobile.Screens.Calendar;

/// <summary>What order the calendar's list is read in - the same three Orbit.Web offers.</summary>
public enum CalendarListSortOrder
{
    /// <summary>
    /// When each thing happens, soonest first. The default, and the one the list exists for: the
    /// question a calendar answers is what is coming.
    /// </summary>
    When,

    /// <summary>
    /// Events first, then deadlines - and within each, still by when. For a reader who came looking for
    /// one kind of thing in a period that holds a lot of both.
    /// </summary>
    Type,

    /// <summary>By name, for finding one thing whose title is what the reader remembers about it.</summary>
    Alphabetical
}

/// <summary>
/// How this reader wants the calendar's list ordered. Kept on the device rather than on the account, as
/// Orbit.Web keeps it in the browser: it describes one page for one reader on one screen and says
/// nothing about what is on it.
/// </summary>
public interface ICalendarListOrderStore
{
    CalendarListSortOrder Read();

    void Write(CalendarListSortOrder sortOrder);

    /// <summary>
    /// Whether the list still shows what is over - a deadline already ticked off, an appointment that
    /// has already ended. False by default, as in the browser: what a calendar is read for is what is
    /// coming, and by the twentieth of a month a month of finished work is what stands in front of it.
    /// </summary>
    bool ReadShowsEverything();

    void WriteShowsEverything(bool showsEverything);
}

/// <summary>
/// One thing on the calendar's list, whichever kind it is. Events and deadlines answer the same
/// question - what is happening in this period - and two lists one under the other made the reader
/// merge them by eye, in a period where they interleave by definition. Orbit.Web's calendar draws them
/// as one list for that reason; this is the same list.
/// </summary>
public sealed record CalendarListEntry
{
    private CalendarListEntry(DateTimeOffset at, string name, CalendarEventRow? calendarEvent, CalendarDeadline? deadline)
    {
        At = at;
        Name = name;
        Event = calendarEvent;
        Deadline = deadline;
    }

    /// <summary>When the thing happens or falls due - what the list is sorted by.</summary>
    public DateTimeOffset At { get; }

    public string Name { get; }

    /// <summary>The appointment this stands for, or null where it stands for a deadline.</summary>
    public CalendarEventRow? Event { get; }

    /// <summary>The entry falling due this stands for, or null where it stands for an appointment.</summary>
    public CalendarDeadline? Deadline { get; }

    public bool IsEvent => Event is not null;

    public bool IsDeadline => Deadline is not null;

    /// <summary>
    /// Whether this is done with: a deadline somebody has ticked off, or an appointment that has already
    /// ended. A deadline that has passed and is still not ticked is <b>not</b> - it is the one thing on
    /// the list that most needs saying, and hiding it would hide the work. The browser draws the same
    /// line - see Orbit.Web's Calendar.razor.
    /// </summary>
    public bool IsOver(DateTimeOffset nowUtc)
        => Deadline?.IsCompleted ?? Event?.EndUtc < nowUtc;

    /// <summary>When it happens, as the row says it - both kinds have one, which is why they share a list.</summary>
    public string When => Event?.When ?? Deadline?.When ?? string.Empty;

    /// <summary>Which list a deadline sits on. Empty for an appointment, which sits on no list.</summary>
    public string ListTitle => Deadline?.ListTitle ?? string.Empty;

    public bool HasListTitle => ListTitle.Length > 0;

    /// <summary>Why this cannot be edited from here just now - see OfflineEditExplanation. Events only.</summary>
    public string Status => Event?.Status ?? string.Empty;

    public bool HasStatus => Status.Length > 0;

    /// <summary>A copy taken while offline, which is otherwise indistinguishable from what it came from.</summary>
    public bool IsCopy => Event?.IsCopy ?? false;

    /// <summary>
    /// Struck through rather than hidden: a day whose errands are all ticked reads differently from an
    /// empty one. False for an appointment, which is not something to tick.
    /// </summary>
    public bool IsDone => Deadline?.IsCompleted ?? false;

    public static CalendarListEntry For(CalendarEventRow calendarEvent)
        => new(calendarEvent.StartUtc, calendarEvent.Title, calendarEvent, null);

    /// <summary>
    /// Sorted by the local date it falls on rather than by a time of day: a deadline has none, and
    /// treating midnight as one would file every deadline before every morning appointment.
    /// </summary>
    public static CalendarListEntry For(CalendarDeadline deadline)
        => new(new DateTimeOffset(deadline.DueLocalDate), deadline.Label, null, deadline);

    /// <summary>
    /// Whichever order the reader chose. When is the fallback within the other two as well: two things
    /// that sort the same by kind or by name are still most usefully read soonest first.
    /// </summary>
    public static IEnumerable<CalendarListEntry> InOrder(
        IEnumerable<CalendarListEntry> entries, CalendarListSortOrder sortOrder) => sortOrder switch
    {
        CalendarListSortOrder.Type => entries
            .OrderBy(entry => entry.IsDeadline)
            .ThenBy(entry => entry.At),
        CalendarListSortOrder.Alphabetical => entries
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.At),
        _ => entries.OrderBy(entry => entry.At)
    };
}
