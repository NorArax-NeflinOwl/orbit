using Orbit.Contracts.Notifications;

namespace Orbit.Web.Services;

/// <summary>
/// One selectable entry in a notification-channel dropdown - pairs the wire value ("None"/"Email"/
/// "Push"/"Both", matching Orbit.Core.Notifications.NotificationChannel on the API side) with the
/// label shown to the user. Shared by CalendarEventEditor.razor, TaskEditor.razor, and
/// InventoryEditor.razor, which all let the user pick a channel per notification trigger.
/// </summary>
public sealed record NotificationChannelOption(string Value, string Label)
{
    public static readonly IReadOnlyList<NotificationChannelOption> All =
    [
        new("None", "None"),
        new("Email", "Email"),
        new("Push", "Push"),
        new("Both", "Email and push")
    ];

    /// <summary>
    /// True when this option requires a delivery channel the user has globally turned off in Options
    /// (see NotificationSettings' class comment on the global switch overriding a per-item choice) -
    /// callers gray the option out rather than removing it, since the item still needs some value
    /// stored even if delivery on that channel is currently suppressed.
    /// </summary>
    public bool IsDisabledBy(NotificationSettingsDto settings)
        => (Value is "Push" or "Both" && (!settings.AllowNotifications || !settings.AllowPush))
            || (Value is "Email" or "Both" && (!settings.AllowNotifications || !settings.AllowEmail));
}
