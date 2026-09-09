namespace Orbit.Data.Entities;

/// <summary>
/// One entry waiting on another entry of the same list - see Orbit.Core.Tasks.TaskItem.WaitsForTaskItemIds.
/// Its own table for the same reason the links to other lists have one: an entry names as many as
/// apply, and a column could hold one.
/// </summary>
public sealed class TaskItemStepEntity
{
    /// <summary>The entry that is waiting.</summary>
    public Guid TaskItemId { get; set; }

    /// <summary>The entry on the same list that has to be done first.</summary>
    public Guid WaitsForTaskItemId { get; set; }

    /// <inheritdoc cref="TaskItemTaskListLinkEntity.Position"/>
    public int Position { get; set; }
}
