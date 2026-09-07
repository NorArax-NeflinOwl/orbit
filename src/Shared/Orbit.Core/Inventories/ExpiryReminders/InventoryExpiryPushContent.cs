using System.Globalization;
using Orbit.Core.Notifications;

namespace Orbit.Core.Inventories.ExpiryReminders;

/// <summary>Builds the push notification payload for an inventory item nearing its expiry date.</summary>
public static class InventoryExpiryPushContent
{
    public static PushNotificationPayload Build(DueExpiryReminder reminder)
    {
        return new PushNotificationPayload(
            "Expiring soon", "\"{0}\" is nearing its expiry date ({1}).",
            [reminder.Name, reminder.ExpiryDate.LocalDateTime.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)],
            // The storage it is on, not the section: every page that reads this can then say *which*
            // one is about to lose something rather than only that something is - which is the
            // difference between a mark on a card and a mark on a page.
            $"/inventory/{reminder.InventoryId}");
    }
}
