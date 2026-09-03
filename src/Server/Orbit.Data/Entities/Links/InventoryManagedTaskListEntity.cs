namespace Orbit.Data.Entities;

/// <summary>
/// Tracks the single system-managed TaskList a given inventory's Inventory feature created for itself -
/// see Orbit.Core.Inventories.IInventoryManagedTaskListRepository. One row per inventory, unique on
/// InventoryId.
/// </summary>
public sealed class InventoryManagedTaskListEntity
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public Guid TaskListId { get; set; }

    /// <summary>Which products the list asks for - see Orbit.Core.Inventories.RestockListSettings.</summary>
    public bool OnlyLinkedWithDueDate { get; set; }

    /// <summary>
    /// When the standing reminder comes round, as minutes past midnight - the same shape
    /// TaskItemEntity.DailyReminderTimeOfDayMinutes uses, and for the same reason: a TimeOnly has no
    /// column type every provider agrees on.
    /// </summary>
    public int RefreshTimeOfDayMinutes { get; set; } = 9 * 60;
}
