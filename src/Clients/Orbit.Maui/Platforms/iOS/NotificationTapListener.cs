using Foundation;
using Orbit.Mobile.Notifications;
using UserNotifications;

namespace Orbit.Maui.Platform;

/// <summary>
/// Notices when the reader taps one of Orbit's notifications, and writes down where it wanted to go.
///
/// It only writes it down. Following it here would be wrong: a tap can launch the app, and this runs
/// before there is a signed-in session or a screen to replace. <see cref="PendingNotificationTap"/>
/// holds it until the app is in a state to act, which is the whole reason that type exists.
///
/// Foreground notifications are deliberately not presented - WillPresentNotification is not overridden,
/// so iOS shows nothing while the app is open. Somebody already looking at Orbit does not need a banner
/// over it, and the in-app feed is where that belongs.
/// </summary>
public sealed class NotificationTapListener : UNUserNotificationCenterDelegate
{
	/// <summary>
	/// The payload key carrying the destination. Set by Orbit.Api's FirebasePushNotificationSender,
	/// which puts it in the aps payload's custom fields because iOS reads it from there rather than from
	/// the shared "data" block Android uses. Changing it on either side breaks tap-through silently.
	/// </summary>
	private const string UrlKey = "url";

	private readonly PendingNotificationTap _pendingTap;

	public NotificationTapListener(PendingNotificationTap pendingTap) => _pendingTap = pendingTap;

	public override void DidReceiveNotificationResponse(
		UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
	{
		_pendingTap.Record(ReadUrl(response.Notification.Request.Content.UserInfo));
		completionHandler();
	}

	/// <summary>
	/// Null for a notification carrying no destination, which is a real case rather than a fault: the
	/// reader still saw it, and the app opens where it normally would.
	/// </summary>
	private static string? ReadUrl(NSDictionary payload)
		=> payload.TryGetValue(new NSString(UrlKey), out var url) ? url?.ToString() : null;
}
