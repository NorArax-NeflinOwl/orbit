using Orbit.Mobile.Chat;

namespace Orbit.Maui.Features.Chat;

/// <summary>
/// What to tell someone whose message was not sent. Without this the text simply vanished from the
/// compose box with nothing said, which reads as the app losing it.
/// </summary>
public static class ChatRefusalMessage
{
	public static string For(ChatSendRefusal refusal) => refusal switch
	{
		ChatSendRefusal.WaitingToBeAccepted => "Accept their chat request first - your message wasn't sent.",
		ChatSendRefusal.SomebodyHasNoChatKey => "Somebody here hasn't set up chat yet, so this couldn't be encrypted.",
		ChatSendRefusal.NoLongerThere => "This conversation is no longer available - your message wasn't sent.",
		_ => "Your message couldn't be sent."
	};
}
