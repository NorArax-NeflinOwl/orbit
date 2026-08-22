using Orbit.Core.Abstractions;

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

    /// <summary>
    /// True for a copy created by accepting another user's share offer (see TaskListShare and
    /// AcceptTaskListShareCommand) - false for a task list the owner created themselves.
    /// <see cref="Update"/> refuses to change a shared copy whose <see cref="AccessLevel"/> is
    /// <see cref="ShareAccessLevel.ReadOnly"/>.
    /// </summary>
    public bool IsShared { get; private set; }

    /// <summary>The sharing user's login, captured once at share-acceptance time. Null when IsShared is false.</summary>
    public string? SharedByUserName { get; private set; }

    /// <summary>The access level the share was accepted under - meaningless when IsShared is false.</summary>
    public ShareAccessLevel AccessLevel { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private TaskList(
        Guid id, Guid userId, string title, IReadOnlyList<TaskItem> items, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel)
    {
        Id = id;
        UserId = userId;
        Title = title;
        Items = items;
        IsCompleted = ComputeIsCompleted(items);
        IsShared = isShared;
        SharedByUserName = sharedByUserName;
        AccessLevel = accessLevel;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static TaskList Create(Guid userId, string title, IReadOnlyList<TaskItem> items)
    {
        var now = DateTimeOffset.UtcNow;
        return new TaskList(Guid.NewGuid(), userId, title, items, now, now, isShared: false, sharedByUserName: null, ShareAccessLevel.ReadOnly);
    }

    /// <summary>
    /// Creates recipientUserId's own copy of a task list once they accept a share - see
    /// AcceptTaskListShareCommandHandler. Each item is recreated with a fresh id and no
    /// <see cref="TaskItem.LinkedTaskListId"/> - a link into the owner's other task lists would be
    /// meaningless (or point at a list the recipient can't even see) once copied into the recipient's
    /// own task lists.
    /// </summary>
    public static TaskList CreateShared(
        Guid recipientUserId, string title, IReadOnlyList<TaskItem> sourceItems, string sharedByUserName, ShareAccessLevel accessLevel)
    {
        var copiedItems = sourceItems
            .Select(item => TaskItem.Create(
                item.Description, item.DueDateUtc, item.IsCompleted, linkedTaskListId: null,
                item.OverdueNotificationChannel, item.RemindDaily, item.DailyReminderNotificationChannel, item.DailyReminderTimeOfDay))
            .ToList();
        var now = DateTimeOffset.UtcNow;
        return new TaskList(Guid.NewGuid(), recipientUserId, title, copiedItems, now, now, isShared: true, sharedByUserName, accessLevel);
    }

    /// <summary>
    /// Rebuilds a task list from already-persisted values, bypassing creation rules.
    /// </summary>
    public static TaskList FromPersistence(
        Guid id, Guid userId, string title, IReadOnlyList<TaskItem> items, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel)
        => new(id, userId, title, items, createdAtUtc, updatedAtUtc, isShared, sharedByUserName, accessLevel);

    /// <summary>
    /// Replaces the title and the whole checklist, then recomputes <see cref="IsCompleted"/> from the
    /// new items.
    /// </summary>
    public void Update(string title, IReadOnlyList<TaskItem> items)
    {
        if (IsShared && AccessLevel == ShareAccessLevel.ReadOnly)
        {
            throw new InvalidOperationException("A shared, read-only task list can't be edited.");
        }

        Title = title;
        Items = items;
        IsCompleted = ComputeIsCompleted(items);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static bool ComputeIsCompleted(IReadOnlyList<TaskItem> items)
        => items.Count > 0 && items.All(item => item.IsCompleted);
}
