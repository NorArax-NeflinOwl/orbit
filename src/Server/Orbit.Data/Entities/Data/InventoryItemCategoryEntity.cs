namespace Orbit.Data.Entities;

/// <summary>
/// One category an <see cref="InventoryItemEntity"/> is filed under. A table rather than the column it
/// used to be, for the same reason <see cref="TaskItemCategoryEntity"/> is one: a shelf item can carry
/// several - see Orbit.Core.Inventories.InventoryItem.Categories - and asking somebody stocking a shelf
/// whether the flour is "baking" or "dry goods" when it is plainly both is a question with no answer.
/// </summary>
public sealed class InventoryItemCategoryEntity
{
    public Guid InventoryItemId { get; set; }

    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Where this category sits among the item's. Stored for the same reason a task entry's position is:
    /// a save deletes the rows and writes them again, so without it the order came back as whatever the
    /// database happened to hold.
    /// </summary>
    public int Position { get; set; }
}
