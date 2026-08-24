using Orbit.Core.Notifications;

namespace Orbit.Core.Inventory.ExpiryReminders;

/// <summary>Builds the push notification payload for an inventory item nearing its expiry date.</summary>
public static class InventoryExpiryPushContent
{
    public static PushNotificationPayload Build(DueExpiryReminder reminder)
    {
        var body = $"\"{reminder.Name}\" is nearing its expiry date ({reminder.ExpiryDate.LocalDateTime:dd.MM.yyyy}).";
        return new PushNotificationPayload("Expiring soon", body, "/inventory");
    }
}
