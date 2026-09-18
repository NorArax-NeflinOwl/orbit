using Orbit.Contracts.Calendar;
using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One item of a task list, as the detail screen shows it. Carries the whole item rather than a few of
/// its fields, because tapping it now opens the rest - see <see cref="TaskItemEditor"/>.
/// </summary>
/// <param name="Detail">
/// Already in the reader's language: when it is due, and whether it says anything about being late or
/// repeats daily. Empty when the entry is only a line of text, which most are.
/// </param>
/// <param name="References">
/// Where an inventory errand points - the shelf it is about, and any other list asking for the same
/// product. Empty for every other kind of entry, which is most of them.
/// </param>
/// <param name="IsWaitingToReachTheServer">
/// True while this entry stands for something made on this phone that the server has not been told
/// about yet - today an appointment written with no connection. Said on the row rather than left to be
/// discovered: an appointment nobody else can see yet is a different thing from one they can, and the
/// difference is invisible otherwise.
/// </param>
public sealed record TaskItemRow(
    TaskItemDto Item, string Detail, bool IsOverdue, IReadOnlyList<TaskItemReference> References,
    bool IsWaitingToReachTheServer = false, string WrittenDescription = "")
{
    /// <param name="appointment">
    /// The event this entry is tied to, where this phone has it. When it happens then comes from there
    /// rather than from the entry's own due date - see <see cref="Describe"/>.
    /// </param>
    public static TaskItemRow From(
        TaskItemDto item, Translations translations, DateTimeOffset nowUtc,
        IReadOnlyList<TaskItemReference>? references = null, bool isWaitingToReachTheServer = false,
        CalendarEventDetailsDto? appointment = null)
        => new(
            item,
            Describe(item, appointment, translations),
            // Only worth saying about something still to do: an entry that is finished with - ticked
            // off or crossed out - cannot be late any more. An appointment is late once it has ended,
            // not once it has begun, which is why this reads the end rather than the start.
            !item.IsCompleted && !item.IsFailed && WhenItIsOver(item, appointment) is { } over && over < nowUtc,
            references ?? [],
            isWaitingToReachTheServer,
            translations.Written(item.Description));

    /// <summary>When there is nothing left of it - the appointment's end, or the entry's own deadline.</summary>
    private static DateTimeOffset? WhenItIsOver(TaskItemDto item, CalendarEventDetailsDto? appointment)
        => appointment?.EndUtc ?? item.DueDateUtc;

    public Guid Id => Item.Id;

    /// <summary>
    /// What the entry says, in the reader's language when Orbit wrote it - see Translations.Written.
    /// Falls back to what is stored, which is what every entry a person typed comes back as anyway.
    /// </summary>
    public string Description => WrittenDescription.Length > 0 ? WrittenDescription : Item.Description;

    public bool IsCompleted => Item.IsCompleted;

    /// <summary>Crossed out rather than ticked off - see Orbit.Core.Tasks.TaskItem.IsFailed.</summary>
    public bool IsFailed => Item.IsFailed;

    /// <summary>
    /// The entries of this same list that have to be done before this one - see
    /// Orbit.Core.Tasks.TaskItem.WaitsForTaskItemIds. Empty for nearly every entry.
    /// </summary>
    public IReadOnlyList<Guid> WaitsForTaskItemIds => Item.AllWaitsForTaskItemIds;

    /// <summary>
    /// Finished with, either way: what the row is struck through for. The circle beside it says which
    /// of the two it was.
    /// </summary>
    public bool IsResolved => IsCompleted || IsFailed;

    /// <summary>
    /// What the entry is filed under, on the row itself: the page filters by these, and a filter whose
    /// subject is invisible on what it returns leaves the reader to take the screen's word for it.
    /// </summary>
    public IReadOnlyList<string> Categories => Item.AllCategories;

    public bool HasCategories => Categories.Count > 0;

    public string CompletionMark => IsCompleted ? "✓" : "○";

    public bool HasDetail => Detail.Length > 0;

    public bool HasReferences => References.Count > 0;

    /// <summary>
    /// The other half of the pair, and it means what it says: the server knows this appointment, because
    /// the entry carries the id the server gave it. Written as its own question rather than as "not
    /// waiting" - an entry that is a Calendar entry with no appointment at all is neither, and calling
    /// that "online" claimed the server knew about something nobody had made.
    /// </summary>
    public bool HasReachedTheServer => Item.LinkedCalendarEventId is not null;

    /// <summary>
    /// What the row says under the words: when it happens, and whether it repeats daily.
    ///
    /// An entry tied to an appointment says the appointment's day and hours rather than its own due
    /// date. The two are not the same thing and drift apart the moment somebody moves the appointment
    /// in a browser, which writes the new time onto the event and leaves the entry's deadline where it
    /// was - the row then said the old day with nothing to say it was old. The entry's page follows the
    /// same rule, and so has Orbit.Web's all along.
    /// </summary>
    private static string Describe(
        TaskItemDto item, CalendarEventDetailsDto? appointment, Translations translations)
    {
        var parts = new List<string>();
        if (appointment is { } tied)
        {
            parts.Add(Calendar.EventWhen.Reads(tied, translations));
        }
        else if (item.DueDateUtc is { } due)
        {
            parts.Add(translations.Format(
                "Due {0}", due.LocalDateTime.ToString("d", translations.DisplayCulture)));
        }

        if (item.RemindDaily)
        {
            parts.Add(translations.Format(
                "Daily at {0}", item.DailyReminderTimeOfDay.ToString("t", translations.DisplayCulture)));
        }

        return string.Join(" · ", parts);
    }
}
