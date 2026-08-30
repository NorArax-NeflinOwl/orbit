using System.Globalization;
using Orbit.Core.Notifications;

namespace Orbit.Core.Inventory.ExpiryReminders;

/// <summary>Builds the push notification payload for an inventory item nearing its expiry date.</summary>
public static class InventoryExpiryPushContent
{
    public static PushNotificationPayload Build(DueExpiryReminder reminder)
    {
        return new PushNotificationPayload(
            "Expiring soon", "\"{0}\" is nearing its expiry date ({1}).",
            [reminder.Name, reminder.ExpiryDate.LocalDateTime.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)],
            "/inventory");
    }
}
