namespace Orbit.Data.Entities;

/// <summary>
/// One task list a <see cref="PlaceEntity"/> belongs to. A table rather than a column, because a place
/// can belong to several - the bakery is on the shopping list and on the Saturday list. Mirrors
/// TaskItemTaskListLinkEntity, which is the same shape one level over.
/// </summary>
public sealed class PlaceTaskListLinkEntity
{
    public Guid PlaceId { get; set; }

    /// <summary>The <see cref="TaskEntity"/> it belongs to.</summary>
    public Guid TaskListId { get; set; }

    /// <inheritdoc cref="TaskItemTaskListLinkEntity.Position"/>
    public int Position { get; set; }
}
