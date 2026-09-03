using Orbit.Core;
using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks;

/// <summary>
/// A single checklist entry within a <see cref="TaskList"/>, with its own due date and completion
/// state - or, if <see cref="LinkedTaskListIds"/> holds anything, a reference to other task lists of
/// the same user instead of an independently completable entry (see
/// <see cref="LinkedTaskCompletionResolver"/> for how its completion is derived, and
/// <see cref="TaskListLinkValidator"/> for how the links themselves are validated).
/// </summary>
public sealed class TaskItem
{
    public Guid Id { get; private set; }
    public string Description { get; private set; }
    public DateTimeOffset? DueDateUtc { get; private set; }
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// The other task lists this entry stands for, in the order somebody put them in. Empty for an
    /// ordinary entry, which is the usual case.
    ///
    /// Several rather than one because a step is often more than one list - "the flat is ready" means
    /// the kitchen and the bathroom and the hall - and writing that as three entries saying the same
    /// thing loses that they are one step. It is done when every list it names is done: any other rule
    /// would let the entry read as finished while work it stands for is still outstanding.
    /// </summary>
    public IReadOnlyList<Guid> LinkedTaskListIds { get; private set; }

    /// <summary>
    /// Whether this entry is a pointer at other lists rather than work of its own. What separates the
    /// two everywhere: a link is not counted as work, not ticked by hand, and not reopened.
    /// </summary>
    public bool IsALinkToOtherLists => LinkedTaskListIds.Count > 0;

    /// <summary>What this entry is and what it stands for - see <see cref="TaskItemSubject"/>.</summary>
    public TaskItemSubject Subject { get; private set; }

    // The four answers above, each still readable on its own, for the same reason the reminders are -
    // everything that acts on them asks one question at a time.

    /// <inheritdoc cref="TaskItemSubject.Kind"/>
    public TaskItemKind Kind => Subject.Kind;

    /// <inheritdoc cref="TaskItemSubject.Location"/>
    public string Location => Subject.Location;

    /// <summary>
    /// The calendar event this entry is the same appointment as, when it is one. The event is where the
    /// place and the time then live, so the two cannot drift apart.
    ///
    /// Not validated: an event that is later deleted leaves this pointing at nothing, and a client
    /// reading it treats that as "no event" - the same way a link to a deleted task list is treated as
    /// "not completed" rather than as a failure (see LinkedTaskCompletionResolver).
    /// </summary>
    public Guid? LinkedCalendarEventId => Subject.LinkedCalendarEventId;

    /// <summary>
    /// The shelf item this entry is an errand about, when it is one - see
    /// <see cref="TaskItemKind.Inventory"/>. Null for every other kind.
    ///
    /// Not validated, for the same reason <see cref="LinkedCalendarEventId"/> is not: an item deleted
    /// afterwards leaves this pointing at nothing, and a reader treats that as "no shelf item" rather
    /// than as a failure.
    /// </summary>
    public Guid? LinkedInventoryItemId => Subject.LinkedInventoryItemId;

    /// <summary>
    /// What this entry is about, in the reader's own words - "shopping", "car", "the flat". Free text
    /// rather than a fixed list, the way a shelf item's category is (see InventoryItem.Category), but
    /// several of them: one errand is often two subjects at once, and being made to pick the single
    /// truest one is how a category stops being written at all.
    ///
    /// Kept for every kind of entry, checklist and appointment alike - what something is about does not
    /// depend on whether it also has a time.
    /// </summary>
    public IReadOnlyList<string> Categories { get; private set; }

    /// <summary>When this entry speaks up and where - see <see cref="TaskItemReminders"/>.</summary>
    public TaskItemReminders Reminders { get; private set; }

    // The four settings above, each still readable on its own. Everything that acts on them - the two
    // schedulers, the repository, the endpoints - asks one question at a time, and making forty read
    // sites say "Reminders." would be a wider change than the one this grouping is for, which is the
    // pile of parameters every way of making an entry had to carry.

    /// <inheritdoc cref="TaskItemReminders.WhenOverdue"/>
    public NotificationChannel OverdueNotificationChannel => Reminders.WhenOverdue;

    /// <inheritdoc cref="TaskItemReminders.Daily"/>
    public bool RemindDaily => Reminders.Daily;

    /// <inheritdoc cref="TaskItemReminders.DailyChannel"/>
    public NotificationChannel DailyReminderNotificationChannel => Reminders.DailyChannel;

    /// <inheritdoc cref="TaskItemReminders.DailyTimeOfDay"/>
    public TimeOnly DailyReminderTimeOfDay => Reminders.DailyTimeOfDay;

    private TaskItem(
        Guid id, string description, DateTimeOffset? dueDateUtc, bool isCompleted, IReadOnlyList<Guid>? linkedTaskListIds,
        TaskItemReminders? reminders, TaskItemSubject? subject, IReadOnlyList<string>? categories)
    {
        Id = id;
        Description = description;
        DueDateUtc = dueDateUtc;
        IsCompleted = isCompleted;
        // Distinct and in order: naming the same list twice is one link written twice, not two steps,
        // and it would make the entry look like it stands for more work than it does.
        LinkedTaskListIds = linkedTaskListIds is null ? [] : [.. linkedTaskListIds.Distinct()];
        Reminders = reminders ?? TaskItemReminders.Default;
        Subject = subject ?? TaskItemSubject.PlainWork;
        Categories = TidyCategories(categories);
    }

    /// <summary>
    /// What is worth storing of what was typed: blanks dropped, edges trimmed, and the same word said
    /// twice kept once - written in the order they were given, because that is the order the reader
    /// thinks of them in. "Shopping" and "shopping" are one category: a filter that told them apart
    /// would quietly hide half of what it was asked for.
    /// </summary>
    private static IReadOnlyList<string> TidyCategories(IReadOnlyList<string>? categories)
        => categories is null
            ? []
            : [.. categories
                .Select(category => category.Trim())
                .Where(category => category.Length > 0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// Brings a finished entry back as something still to do, keeping its identity - the same row the
    /// reader already knows, rather than a second one beside it. Used where a task is meant to recur:
    /// an inventory item that is low again, and a daily reminder coming round.
    ///
    /// A linked entry is left alone: its completion follows the list it links to, and forcing it here
    /// would be overwritten by the next resolve anyway.
    /// </summary>
    public void Reopen()
    {
        if (!IsALinkToOtherLists)
        {
            IsCompleted = false;
        }
    }

    /// <summary>
    /// Crosses an entry off. Used where something other than the reader's own tick establishes that the
    /// work is done - an inventory that turns out to hold what the entry asks for.
    ///
    /// A linked entry is left alone for the same reason <see cref="Reopen"/> leaves it: its completion
    /// follows the list it points at.
    /// </summary>
    public void Complete()
    {
        if (!IsALinkToOtherLists)
        {
            IsCompleted = true;
        }
    }

    /// <summary>
    /// Keeps what this entry is already filed under, for a caller that said nothing about it - see
    /// UpdateTaskListCommand.EntriesKeepingTheirCategories.
    /// </summary>
    public void KeepCategoriesOf(TaskItem stored) => Categories = stored.Categories;

    /// <summary>
    /// A linked item's completion can't be set directly - it always follows the lists it links to (see
    /// <see cref="LinkedTaskCompletionResolver"/>) - so <paramref name="isCompleted"/> is ignored in
    /// favor of "not completed" whenever <paramref name="linkedTaskListIds"/> holds anything.
    /// </summary>
    public static TaskItem Create(
        string description, DateTimeOffset? dueDateUtc, bool isCompleted, IReadOnlyList<Guid>? linkedTaskListIds = null,
        TaskItemReminders? reminders = null, TaskItemSubject? subject = null, IReadOnlyList<string>? categories = null)
    {
        // Here rather than in the constructor, which FromPersistence also uses: a row already stored
        // fits by definition, and rejecting one on the way back out would make an old entry unreadable
        // rather than telling anybody anything.
        StoredTextLimits.OrRefuse(description, StoredTextLimits.TaskDescription, "task entry");
        // The place as the subject actually keeps it: an address too long to store is refused, and one
        // an entry of this kind does not keep at all was already dropped - see TaskItemSubject.
        StoredTextLimits.OrRefuse(subject?.Location ?? string.Empty, StoredTextLimits.Address, "place's address");
        foreach (var category in categories ?? [])
        {
            StoredTextLimits.OrRefuse(category, StoredTextLimits.Category, "task entry's category");
        }

        return new TaskItem(
            Guid.NewGuid(), description, dueDateUtc,
            (linkedTaskListIds is null || linkedTaskListIds.Count == 0) && isCompleted, linkedTaskListIds,
            reminders, subject, categories);
    }

    /// <summary>
    /// The same entry under a new name. Used when two clients hand over the same id and neither may keep
    /// it - see <see cref="TaskItemIdentity"/>. Everything else travels: what the entry is does not
    /// change, only what it is called.
    /// </summary>
    public TaskItem WithNewId()
        => new(
            Guid.NewGuid(), Description, DueDateUtc, IsCompleted, LinkedTaskListIds,
            Reminders, Subject, Categories);

    /// <summary>
    /// Rebuilds a checklist entry from already-known values, bypassing the completion override above -
    /// used both to reload an entry as persisted, and by <see cref="LinkedTaskCompletionResolver"/> to
    /// apply a freshly resolved completion value to a linked entry.
    /// </summary>
    public static TaskItem FromPersistence(
        Guid id, string description, DateTimeOffset? dueDateUtc, bool isCompleted, IReadOnlyList<Guid>? linkedTaskListIds,
        TaskItemReminders? reminders, TaskItemSubject? subject = null, IReadOnlyList<string>? categories = null)
        => new(id, description, dueDateUtc, isCompleted, linkedTaskListIds, reminders, subject, categories);
}
