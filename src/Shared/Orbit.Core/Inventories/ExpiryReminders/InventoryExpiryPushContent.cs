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

    /// <summary>
    /// One warning for everything of an owner's that came near its date in the same poll - the second
    /// warning of a minute is the one nobody sees, for the reason Orbit.Core.Tasks.SeveralEntriesAtOnce
    /// gives. A single thing says what it always said, row mark and all.
    ///
    /// Each thing is named with its date, since two of them rarely go off on the same day and the date
    /// is what says which to use first. It leads to the one storage they are all on, without a row
    /// picked out - a mark on one row would say the others did not matter - or to the storages
    /// themselves when they are on several.
    /// </summary>
    public static PushNotificationPayload Build(IReadOnlyList<DueExpiryReminder> reminders)
    {
        if (reminders is [var theOnlyOne])
        {
            return Build(theOnlyOne);
        }

        var named = string.Join(", ", reminders.Select(reminder =>
            $"{reminder.Name} ({reminder.ExpiryDate.LocalDateTime.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)})"));
        var inventoryIds = reminders.Select(reminder => reminder.InventoryId).Distinct().ToList();
        return new PushNotificationPayload(
            "Things expiring soon", "These are nearing their expiry date: {0}.", [named],
            inventoryIds is [var theOnlyInventory] ? $"/inventory/{theOnlyInventory}" : "/inventory");
    }
}
