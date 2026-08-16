namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a single checklist entry within a <see cref="TaskEntity"/>.
/// </summary>
public sealed class TaskItemEntity
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset? DueDateUtc { get; set; }
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Id of another <see cref="TaskEntity"/> this entry references instead of being independently
    /// completable - see <see cref="Orbit.Core.Tasks.LinkedTaskCompletionResolver"/>.
    /// </summary>
    public Guid? LinkedTaskListId { get; set; }
}
