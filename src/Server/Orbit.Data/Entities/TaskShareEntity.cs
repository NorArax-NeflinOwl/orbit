namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of <see cref="Orbit.Core.Tasks.TaskListShare"/>, mapped separately so schema
/// changes don't force changes onto domain logic, and vice versa. Named "TaskShareEntity" rather than
/// "TaskListShareEntity" to match <see cref="TaskEntity"/> (the persistence shape of a "TaskList"),
/// mirrors CalendarEventShareEntity.
/// </summary>
public sealed class TaskShareEntity
{
    public Guid Id { get; set; }
    public Guid SourceTaskListId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public string AccessLevel { get; set; } = "ReadOnly";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
}
