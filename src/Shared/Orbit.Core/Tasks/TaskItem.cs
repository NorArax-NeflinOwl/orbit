namespace Orbit.Core.Tasks;

/// <summary>
/// A single checklist entry within a <see cref="TaskList"/>, with its own due date and completion
/// state - or, if <see cref="LinkedTaskListId"/> is set, a reference to another of the user's task
/// lists instead of an independently completable entry (see <see cref="LinkedTaskCompletionResolver"/>
/// for how its completion is derived, and <see cref="TaskListLinkValidator"/> for how the link itself
/// is validated).
/// </summary>
public sealed class TaskItem
{
    public Guid Id { get; private set; }
    public string Description { get; private set; }
    public DateTimeOffset? DueDateUtc { get; private set; }
    public bool IsCompleted { get; private set; }
    public Guid? LinkedTaskListId { get; private set; }

    private TaskItem(Guid id, string description, DateTimeOffset? dueDateUtc, bool isCompleted, Guid? linkedTaskListId)
    {
        Id = id;
        Description = description;
        DueDateUtc = dueDateUtc;
        IsCompleted = isCompleted;
        LinkedTaskListId = linkedTaskListId;
    }

    /// <summary>
    /// A linked item's completion can't be set directly - it always follows the list it links to (see
    /// <see cref="LinkedTaskCompletionResolver"/>) - so <paramref name="isCompleted"/> is ignored in
    /// favor of "not completed" whenever <paramref name="linkedTaskListId"/> is set.
    /// </summary>
    public static TaskItem Create(string description, DateTimeOffset? dueDateUtc, bool isCompleted, Guid? linkedTaskListId = null)
        => new(Guid.NewGuid(), description, dueDateUtc, linkedTaskListId is null && isCompleted, linkedTaskListId);

    /// <summary>
    /// Rebuilds a checklist entry from already-known values, bypassing the completion override above -
    /// used both to reload an entry as persisted, and by <see cref="LinkedTaskCompletionResolver"/> to
    /// apply a freshly resolved completion value to a linked entry.
    /// </summary>
    public static TaskItem FromPersistence(Guid id, string description, DateTimeOffset? dueDateUtc, bool isCompleted, Guid? linkedTaskListId)
        => new(id, description, dueDateUtc, isCompleted, linkedTaskListId);
}
