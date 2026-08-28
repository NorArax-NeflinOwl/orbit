using Orbit.Core.Notifications;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens;

/// <summary>
/// One way of being told about something, paired with what to call it. The wire wants Orbit.Core's own
/// word - "None", "Email", "Push", "Both" - and a reader wants a sentence: "Both" is shown as "email
/// and push", which is what Orbit.Web's dropdowns say. The phone showed the raw enum names.
/// </summary>
public sealed record NotificationChannelChoice(string Value, string Name)
{
    public static IReadOnlyList<NotificationChannelChoice> All(Translations translations)
        =>
        [
            new(nameof(NotificationChannel.None), translations["None"]),
            new(nameof(NotificationChannel.Email), translations["Email"]),
            new(nameof(NotificationChannel.Push), translations["Push"]),
            new(nameof(NotificationChannel.Both), translations["Email and push"])
        ];

    /// <summary>The one whose wire value this is, or the first - a stored value is always one of them.</summary>
    public static NotificationChannelChoice For(IReadOnlyList<NotificationChannelChoice> all, string value)
        => all.FirstOrDefault(choice => choice.Value == value) ?? all[0];
}
