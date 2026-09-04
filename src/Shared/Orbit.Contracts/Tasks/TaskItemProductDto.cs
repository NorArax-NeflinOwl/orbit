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
/// <param name="Unit">Serialized Orbit.Core.Inventories.InventoryUnit - what the two amounts are counted in.</param>
/// <param name="ExpiryNotificationChannel">One of "None"/"Email"/"Push"/"Both" - matches Orbit.Core.Notifications.NotificationChannel.</param>
public sealed record TaskItemProductDto(
    string ProductType,
    string Category,
    decimal Quantity,
    decimal? MinimumQuantity,
    string Unit,
    DateTimeOffset? ExpiryDate,
    string ExpiryNotificationChannel,
    bool IsCheckedRegularly);
