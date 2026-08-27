using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// What to tell someone whose edit or delete did not happen. Being offline is worth saying out loud
/// here, unlike when sending: an edit is not queued, so nothing will pick it up later.
/// </summary>
public static class ChatEditMessage
{
    public static string For(ChatEditOutcome outcome, Translations translations) => outcome switch
    {
        ChatEditOutcome.Offline => translations["Changing a message needs a connection."],
        ChatEditOutcome.NotAllowed => translations["That message can't be changed any more."],
        ChatEditOutcome.SomebodyHasNoChatKey
            => translations["Somebody here hasn't set up chat, so it couldn't be re-encrypted."],
        _ => string.Empty
    };
}
