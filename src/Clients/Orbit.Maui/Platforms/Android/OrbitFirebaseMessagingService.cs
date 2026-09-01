using Android.App;
using Firebase.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Mobile.Notifications;

namespace Orbit.Maui.Platform;

/// <summary>
/// What happens to a push that arrives while somebody is looking at the app.
///
/// Firebase shows Orbit's messages itself - they carry a notification block - but only while the app is
/// in the background. In the foreground it hands the message here instead and shows nothing, so a
/// notification arriving with Orbit open was silently dropped: the feed had it on the next read, and the
/// moment it happened passed unremarked.
///
/// This does not post to the tray. A heads-up notification over the very screen the message is about is
/// noise; the banner the app draws for itself is the answer, and it is the one the browser gives too -
/// see ForegroundNotices, which also holds the settings deciding whether it appears at all.
/// </summary>
[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class OrbitFirebaseMessagingService : FirebaseMessagingService
{
	public override void OnMessageReceived(RemoteMessage message)
	{
		base.OnMessageReceived(message);

		// A message with nothing to read is not worth a banner. Data-only sends do not happen today -
		// Orbit always writes a notification block - but one arriving would land here too.
		if (message.GetNotification() is not { } notification)
		{
			return;
		}

		if (IPlatformApplication.Current?.Services.GetService<ForegroundNotices>() is not { } notices)
		{
			return;
		}

		var notice = new ForegroundNotice(
			notification.Title ?? string.Empty,
			notification.Body ?? string.Empty,
			message.Data.TryGetValue("url", out var url) ? url : null);

		// Fire and forget on purpose: this runs on Firebase's own callback, and holding it while the
		// settings are fetched would delay every message behind this one.
		_ = MainThread.InvokeOnMainThreadAsync(() => notices.ShowAsync(notice));
	}

	/// <summary>
	/// A token can be replaced without anybody signing in again - Play Services reissues them. The
	/// registration itself belongs to PushRegistration, which runs on every sign-in; this only makes
	/// sure a token that changed mid-session is not the one the server keeps sending to.
	/// </summary>
	public override void OnNewToken(string token)
	{
		base.OnNewToken(token);

		if (IPlatformApplication.Current?.Services.GetService<PushRegistration>() is { } registration)
		{
			_ = registration.RegisterThisDeviceAsync();
		}
	}
}
