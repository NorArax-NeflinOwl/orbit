namespace Orbit.Web.Services;

/// <summary>
/// One selectable entry in a notification-channel dropdown - pairs the wire value ("None"/"Email"/
/// "Push"/"Both", matching Orbit.Core.Notifications.NotificationChannel on the API side) with the
/// Polish label shown to the user. Shared by CalendarEventEditor.razor and TaskEditor.razor, which both
/// let the user pick a channel per notification trigger.
/// </summary>
public sealed record NotificationChannelOption(string Value, string Label)
{
    public static readonly IReadOnlyList<NotificationChannelOption> All =
    [
        new("None", "Brak"),
        new("Email", "E-mail"),
        new("Push", "Push"),
        new("Both", "E-mail i push")
    ];
}
