namespace Orbit.Core.Inventories.ExpiryReminders;

/// <summary>Builds the subject and body of an e-mail sent when an inventory item is nearing its expiry date.</summary>
public static class InventoryExpiryEmailContent
{
    public static (string Subject, string Body) Build(DueExpiryReminder reminder)
    {
        var subject = $"Expiring soon: {reminder.Name}";
        var body =
            $"\"{reminder.Name}\" in your inventory is nearing its expiry date." +
            $"{Environment.NewLine}Expires: {reminder.ExpiryDate.LocalDateTime:dd.MM.yyyy}";

        return (subject, body);
    }

    /// <summary>
    /// One e-mail for everything that came near its date in the same poll, a line per thing - the same
    /// gathering InventoryExpiryPushContent does. A single thing keeps the mail it always had.
    /// </summary>
    public static (string Subject, string Body) Build(IReadOnlyList<DueExpiryReminder> reminders)
    {
        if (reminders is [var theOnlyOne])
        {
            return Build(theOnlyOne);
        }

        var subject = $"{reminders.Count} things expiring soon";
        var body = string.Join(Environment.NewLine, [
            "These things in your inventory are nearing their expiry date:",
            .. reminders.Select(reminder => $"- \"{reminder.Name}\" (expires: {reminder.ExpiryDate.LocalDateTime:dd.MM.yyyy})")]);

        return (subject, body);
    }
}
