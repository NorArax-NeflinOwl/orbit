namespace Orbit.Core.Inventory.ExpiryReminders;

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
}
