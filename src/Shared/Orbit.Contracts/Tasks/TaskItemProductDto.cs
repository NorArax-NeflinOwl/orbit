namespace Orbit.Contracts.Tasks;

/// <summary>
/// What an inventory entry asks for, in the detail a shelf item is kept in - see
/// Orbit.Core.Tasks.TaskItemProduct.
///
/// Sent for an entry of the Inventory kind that does not yet stand for a real shelf item; ignored for
/// every other entry, and for one carrying a LinkedInventoryItemId, since the shelf item is then the
/// answer. Null on the way in means "not provided", and leaves whatever is stored alone - the same rule
/// the categories follow, so a client written before this existed cannot empty it by saving a list.
/// </summary>
/// <param name="Categories">
/// What it is filed under, as many words as apply - the same shape a shelf item's own categories travel
/// in (see InventoryItemDto.Categories), because this becomes one. Null and empty both mean "filed under
/// nothing"; the whole product is what a client either sends or leaves out, not each field of it.
/// </param>
/// <param name="Unit">Serialized Orbit.Core.Inventories.InventoryUnit - what the two amounts are counted in.</param>
/// <param name="ExpiryNotificationChannel">One of "None"/"Email"/"Push"/"Both" - matches Orbit.Core.Notifications.NotificationChannel.</param>
public sealed record TaskItemProductDto(
    string ProductType,
    IReadOnlyList<string>? Categories,
    decimal Quantity,
    decimal? MinimumQuantity,
    string Unit,
    DateTimeOffset? ExpiryDate,
    string ExpiryNotificationChannel,
    bool IsCheckedRegularly)
{
    /// <summary>The categories as something to read without a null check - see <see cref="Categories"/>.</summary>
    public IReadOnlyList<string> AllCategories => Categories ?? [];
}
