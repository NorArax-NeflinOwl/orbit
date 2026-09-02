namespace Orbit.Core.Tasks;

/// <summary>
/// What an entry is, and the thing it stands for: plain work, an appointment - with a place written on
/// it, or an event in the calendar that holds the place instead - or an errand about something on a
/// shelf. One object rather than four parameters, because none of the four means anything without the
/// others: which link is kept depends on the kind, and whether a place is kept depends on the link.
///
/// Those rules live here rather than in <see cref="TaskItem"/>'s constructor, which is where they were:
/// they are rules about this answer, not about the entry that carries it, and putting them beside the
/// data they govern is what stops an entry being built in a state the rules forbid.
/// </summary>
public sealed record TaskItemSubject
{
    public TaskItemSubject(
        TaskItemKind kind, string location = "", Guid? linkedCalendarEventId = null, Guid? linkedInventoryItemId = null)
    {
        Kind = kind;
        // A link only means something for the kind it belongs to. Kept this way rather than refused, so
        // switching an entry's kind drops what no longer applies instead of failing the save.
        LinkedCalendarEventId = kind == TaskItemKind.Calendar ? linkedCalendarEventId : null;
        LinkedInventoryItemId = kind == TaskItemKind.Inventory ? linkedInventoryItemId : null;
        // Only an appointment has a place at all, and one tied to an event keeps none: the event holds
        // the place, and storing it twice is how the two come to disagree - which is the whole reason
        // the link exists.
        Location = kind == TaskItemKind.Calendar && LinkedCalendarEventId is null ? location.Trim() : string.Empty;
    }

    /// <summary>Which of the three an entry is - see <see cref="TaskItemKind"/>.</summary>
    public TaskItemKind Kind { get; }

    /// <summary>Where an appointment happens, as the reader wrote it. Empty for everything else.</summary>
    public string Location { get; }

    /// <summary>The calendar event this entry is the same appointment as, when it is one.</summary>
    public Guid? LinkedCalendarEventId { get; }

    /// <summary>The shelf item this entry is an errand about, when it is one.</summary>
    public Guid? LinkedInventoryItemId { get; }

    /// <summary>Work with no time and no shelf behind it, which is what most entries are.</summary>
    public static readonly TaskItemSubject PlainWork = new(TaskItemKind.Checklist);
}
