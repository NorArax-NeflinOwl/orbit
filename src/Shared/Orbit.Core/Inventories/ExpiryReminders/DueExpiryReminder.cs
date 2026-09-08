using Orbit.Core.Notifications;

namespace Orbit.Core.Inventories.ExpiryReminders;

/// <summary>
/// A single inventory item nearing (or past) its expiry date, carrying just enough to build and route
/// a warning about it - a lighter-weight projection than the full InventoryItem, mirroring
/// Orbit.Core.Tasks.OverdueNotifications.OverdueTaskItem.
/// </summary>
/// <param name="InventoryId">
/// The storage the item is on. Carried so the warning can name it: a notification that says only
/// "/inventory" leaves every page that reads it able to say something is about to go off and unable to
/// say where - see InventoryExpiryPushContent.
/// </param>
public sealed record DueExpiryReminder(
    Guid InventoryItemId,
    Guid InventoryId,
    Guid UserId,
    string Name,
    DateTimeOffset ExpiryDate,
    NotificationChannel NotificationChannel);
