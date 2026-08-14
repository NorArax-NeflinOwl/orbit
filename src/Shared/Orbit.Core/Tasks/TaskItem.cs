namespace Orbit.Core.Tasks;

/// <summary>
/// A single checklist entry within a <see cref="TaskList"/>, with its own due date and completion
/// state.
/// </summary>
public sealed class TaskItem
{
    public Guid Id { get; private set; }
    public string Description { get; private set; }
    public DateTimeOffset? DueDateUtc { get; private set; }
    public bool IsCompleted { get; private set; }

    private TaskItem(Guid id, string description, DateTimeOffset? dueDateUtc, bool isCompleted)
    {
        Id = id;
        Description = description;
        DueDateUtc = dueDateUtc;
        IsCompleted = isCompleted;
    }

    public static TaskItem Create(string description, DateTimeOffset? dueDateUtc, bool isCompleted)
        => new(Guid.NewGuid(), description, dueDateUtc, isCompleted);

    /// <summary>
    /// Rebuilds a checklist entry from already-persisted values, bypassing creation rules.
    /// </summary>
    public static TaskItem FromPersistence(Guid id, string description, DateTimeOffset? dueDateUtc, bool isCompleted)
        => new(id, description, dueDateUtc, isCompleted);
}
