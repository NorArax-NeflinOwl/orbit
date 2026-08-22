namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a task list, mapped separately from <see cref="Orbit.Core.Tasks.TaskList"/> so
/// schema changes don't force changes onto domain logic, and vice versa.
/// </summary>
public sealed class TaskEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }

    /// <summary>True for a copy created by accepting a share offered by another user.</summary>
    public bool IsShared { get; set; }

    /// <summary>The sharing user's login, captured once at share-acceptance time. Null when IsShared is false.</summary>
    public string? SharedByUserName { get; set; }

    /// <summary>"ReadOnly" or "CanEdit", captured once at share-acceptance time. Meaningless when IsShared is false.</summary>
    public string AccessLevel { get; set; } = "ReadOnly";

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public List<TaskItemEntity> Items { get; set; } = [];
}
