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
            //
            // And the row itself, named the way everything else in Orbit names a row it wants landed on
            // (see TaskListChecklist's links to a shelf item). Following the warning opens the shelf with
            // that row picked out, and the shelf marks it - a card saying "something happened here" over
            // thirty rows still left the reader to find which. The page is still the page: the address
            // that settles this entry is the one before the "?" - see NotificationUrl.
            NotificationUrl.Naming($"/inventory/{reminder.InventoryId}", reminder.InventoryItemId));
    }
}
