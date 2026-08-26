using Orbit.Mobile.Chat;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// What to tell someone whose edit or delete did not happen. Being offline is worth saying out loud
/// here, unlike when sending: an edit is not queued, so nothing will pick it up later.
/// </summary>
public static class ChatEditMessage
{
    public static string For(ChatEditOutcome outcome) => outcome switch
    {
        ChatEditOutcome.Offline => "Changing a message needs a connection.",
        ChatEditOutcome.NotAllowed => "That message can't be changed any more.",
        ChatEditOutcome.SomebodyHasNoChatKey => "Somebody here hasn't set up chat, so it couldn't be re-encrypted.",
        _ => string.Empty
    };
}
