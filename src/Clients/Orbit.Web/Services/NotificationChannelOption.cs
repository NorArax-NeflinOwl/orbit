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
    /// <summary>
    /// "None" is called Banner because that is what it actually does. Every notification is written
    /// into the in-app feed whichever channel is chosen - see NotificationRecorder, which records the
    /// entry before the channel is even consulted - so "None" never meant silence, it meant "the
    /// banner and nothing else". Naming it after the delivery it leaves out rather than the one it
    /// keeps made the quietest option read as the broken one.
    /// </summary>
    public static readonly IReadOnlyList<NotificationChannelOption> All =
    [
        new("None", "Banner only"),
        new("Push", "Push"),
        new("Email", "Email"),
        new("Both", "Push and email")
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
