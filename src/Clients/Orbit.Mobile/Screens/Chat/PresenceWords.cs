using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// Where somebody is, said in words - the same four the coloured dot beside their name means, and the
/// same four Orbit.Web writes. See Orbit.Core.Users.PresenceStatus, which is what the server sends.
///
/// One place rather than one per screen: a contact's own page and the conversation with them both say
/// it, and two copies of a switch over the same four names is how two screens end up disagreeing about
/// what "away" is called.
/// </summary>
public static class PresenceWords
{
    /// <param name="status">
    /// The server's own name for it. Anything else - including nothing at all, which is what a contact
    /// synced before presence existed carries - reads as offline, because not knowing where somebody is
    /// and their not being anywhere look the same from here.
    /// </param>
    public static string Describe(string status, Translations translations)
        => translations[status switch
        {
            nameof(Core.Users.PresenceStatus.Available) => "Available",
            nameof(Core.Users.PresenceStatus.Away) => "Away",
            nameof(Core.Users.PresenceStatus.DoNotDisturb) => "Do not disturb",
            _ => "Offline"
        }];
}
