namespace Orbit.Contracts.Inventories;

public sealed record InventoryItemDto(
    Guid Id,
    string Name,
    string ProductType,
    /// <summary>
    /// The first thing it is filed under, or empty. The old shape, kept because a client that has not
    /// learned about the new one still reads and writes this - see <see cref="Categories"/>. Mirrors
    /// how TaskItemDto carries LinkedTaskListId beside LinkedTaskListIds, and for the same reason.
    /// </summary>
    string Category,
    decimal Quantity,
    decimal? MinimumQuantity,
    /// <summary>Serialized Orbit.Core.Inventories.InventoryUnit - what the two amounts above are counted in.</summary>
    string Unit,
    DateTimeOffset? ExpiryDate,
    string ExpiryNotificationChannel,
    bool IsBelowMinimum,
    bool HasPendingRestockTask,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    /// <summary>
    /// Something to look at every round rather than only when it runs low - see
    /// Orbit.Core.Inventories.InventoryItem.IsCheckedRegularly.
    /// </summary>
    bool IsCheckedRegularly = false,
    /// <summary>
    /// Everything it is filed under, in order - see Orbit.Core.Inventories.InventoryItem.Categories.
    /// Always sent; <see cref="Category"/> above repeats the first of them for older clients.
    /// </summary>
    IReadOnlyList<string>? Categories = null)
{
    /// <summary>Whichever shape the sender used, read as one - the same helper TaskItemDto carries.</summary>
    public IReadOnlyList<string> AllCategories
        => Categories is { Count: > 0 } categories ? categories : Category.Length > 0 ? [Category] : [];
}
