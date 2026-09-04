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

    /// <summary>
    /// Whether this inventory keeps a restock list at all - see
    /// Orbit.Core.Inventories.RestockListSettings.IsEnabled. Defaulted true in the schema as well as
    /// here, so every row written before this column existed reads back as the behaviour it had.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Whether the list carries the standing daily reminder - see RestockListSettings.RemindDaily.</summary>
    public bool RemindDaily { get; set; } = true;

    /// <summary>Stored by name, like every other enum here - see Orbit.Core.Abstractions.ItemPriority.</summary>
    public string ListPriority { get; set; } = nameof(Orbit.Core.Abstractions.ItemPriority.Normal);
}
