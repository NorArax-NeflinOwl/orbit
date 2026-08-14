namespace Orbit.Core.Tasks;

/// <summary>
/// A titled checklist owned by a user. Named "TaskList" rather than "Task" to avoid colliding with
/// <see cref="System.Threading.Tasks.Task"/>, which every async method in this codebase returns.
/// </summary>
public sealed class TaskList
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; }
    public IReadOnlyList<TaskItem> Items { get; private set; }

    /// <summary>
    /// Derived, not settable directly: a task list is done exactly when every item on it is checked
    /// off, and an empty list is never considered done.
    /// </summary>
    public bool IsCompleted { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private TaskList(
        Guid id, Guid userId, string title, IReadOnlyList<TaskItem> items,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        Title = title;
        Items = items;
        IsCompleted = ComputeIsCompleted(items);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static TaskList Create(Guid userId, string title, IReadOnlyList<TaskItem> items)
    {
        var now = DateTimeOffset.UtcNow;
        return new TaskList(Guid.NewGuid(), userId, title, items, now, now);
    }

    /// <summary>
    /// Rebuilds a task list from already-persisted values, bypassing creation rules.
    /// </summary>
    public static TaskList FromPersistence(
        Guid id, Guid userId, string title, IReadOnlyList<TaskItem> items,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
        => new(id, userId, title, items, createdAtUtc, updatedAtUtc);

    /// <summary>
    /// Replaces the title and the whole checklist, then recomputes <see cref="IsCompleted"/> from the
    /// new items.
    /// </summary>
    public void Update(string title, IReadOnlyList<TaskItem> items)
    {
        Title = title;
        Items = items;
        IsCompleted = ComputeIsCompleted(items);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static bool ComputeIsCompleted(IReadOnlyList<TaskItem> items)
        => items.Count > 0 && items.All(item => item.IsCompleted);
}
