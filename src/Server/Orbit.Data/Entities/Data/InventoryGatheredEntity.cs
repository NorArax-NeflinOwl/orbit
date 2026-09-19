namespace Orbit.Data.Entities;

/// <summary>
/// One shelf an <see cref="InventoryEntity"/> gathers - see
/// Orbit.Core.Inventories.Inventory.GathersInventoryIds. A table rather than a column, because a group
/// gathers as many as somebody arranged, the same way <see cref="TaskItemTaskListLinkEntity"/> holds the
/// lists an entry stands for.
/// </summary>
public sealed class InventoryGatheredEntity
{
    /// <summary>The group.</summary>
    public Guid InventoryId { get; set; }

    /// <summary>The <see cref="InventoryEntity"/> being gathered.</summary>
    public Guid GatheredInventoryId { get; set; }

    /// <summary>
    /// Where this member sits among the group's. Stored for the same reason a task entry's links carry
    /// one: a save deletes the rows and writes them again, so without it the order came back as whatever
    /// the database happened to hold - and the order is what somebody arranged.
    /// </summary>
    public int Position { get; set; }
}
