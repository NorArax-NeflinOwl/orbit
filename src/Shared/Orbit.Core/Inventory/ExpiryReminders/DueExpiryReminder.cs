using Orbit.Core.Notifications;

namespace Orbit.Core.Inventory.ExpiryReminders;

/// <summary>
/// A single inventory item nearing (or past) its expiry date, carrying just enough to build and route
/// a warning about it - a lighter-weight projection than the full InventoryItem, mirroring
/// Orbit.Core.Tasks.OverdueNotifications.OverdueTaskItem.
/// </summary>
public sealed record DueExpiryReminder(
    Guid InventoryItemId,
    Guid UserId,
    string Name,
    DateTimeOffset ExpiryDate,
    NotificationChannel NotificationChannel);
