namespace Orbit.Data.Entities;

/// <summary>
/// One list a <see cref="TaskItemEntity"/> stands for. A table rather than a column on the entry
/// itself, because an entry can name several - see Orbit.Core.Tasks.TaskItem.LinkedTaskListIds.
/// </summary>
public sealed class TaskItemTaskListLinkEntity
{
    public Guid TaskItemId { get; set; }

    /// <summary>The <see cref="TaskEntity"/> being pointed at.</summary>
    public Guid LinkedTaskListId { get; set; }

    /// <summary>
    /// Where this link sits among the entry's links. Stored for the same reason the entry's own
    /// position is: a save deletes the rows and writes them again, so without it the order came back as
    /// whatever the database happened to hold.
    /// </summary>
    public int Position { get; set; }
}
