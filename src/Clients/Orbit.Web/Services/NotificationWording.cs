using Orbit.Contracts.Notifications;

namespace Orbit.Web.Services;

/// <summary>
/// Turns one notification into the sentence a reader sees. The server sends the English sentence and
/// what fills its holes rather than a finished one, because it has no idea what language this browser
/// is set to - it is a preference the device keeps for itself. See Orbit.Core's PushNotificationPayload.
///
/// Shared rather than written where it is needed, because it was written in one of the two places that
/// need it: the panel filled the holes and the banner printed the sentence raw, so a reminder that read
/// "Wydarzenie „Dentysta” zaczyna się za 30 min." in the bell slid past the top of the screen as
/// 'The event "{0}" starts in {1} min.'
/// </summary>
public static class NotificationWording
{
    /// <summary>
    /// An entry with no arguments is looked up whole: that is what a phrase like "New message" is, and
    /// what every entry written before the server split them apart looks like.
    /// </summary>
    public static string Say(Translations translations, string format, IReadOnlyList<string>? arguments)
        => arguments is { Count: > 0 }
            ? translations.Format(format, [.. arguments])
            : translations[format];

    /// <summary>The two halves of one entry, said the same way - what a banner and a row both need.</summary>
    public static string Title(Translations translations, NotificationEntryDto entry)
        => Say(translations, entry.Title, entry.TitleArguments);

    public static string Body(Translations translations, NotificationEntryDto entry)
        => Say(translations, entry.Body, entry.BodyArguments);
}
