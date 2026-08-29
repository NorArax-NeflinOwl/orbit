using Orbit.Core.Mobile;
using Orbit.Mobile.Notifications;

namespace Orbit.Maui.Platform;

/// <summary>
/// Asks Android for permission to notify, and would obtain the token Orbit sends to.
///
/// The same shape as the iOS implementation and for the same reasons, with one difference worth
/// knowing: Orbit's server delivers through Firebase Cloud Messaging, which reaches Android directly
/// rather than through a second service underneath. Android therefore needs no APNs auth key, and is
/// the platform where push can work end to end first - see info/orbit-maui-plan.md §4.2.
///
/// The token half is still missing here, and says so rather than inventing something: an FCM
/// registration token needs the Firebase SDK in the app and a google-services.json registered for
/// this build's application id. Until both exist, <see cref="PushRegistrationOutcome.NotAvailableHere"/>
/// is the honest answer - a device registered with a token nothing can deliver to is counted reachable
/// by the server, which is invisible from there.
///
/// What replaces this: obtain the FCM token and return it with
/// <see cref="PushRegistrationOutcome.Registered"/>. Nothing above this class changes.
/// </summary>
public sealed class PhonePushNotifications : IDevicePushNotifications
{
	public string Platform => nameof(MobilePlatform.Android);

	public async Task<PushRegistrationResult> RegisterAsync(CancellationToken cancellationToken = default)
	{
		if (!await IsPermittedAsync())
		{
			return new PushRegistrationResult(PushRegistrationOutcome.NotPermitted);
		}

		return new PushRegistrationResult(PushRegistrationOutcome.NotAvailableHere);
	}

	/// <summary>
	/// POST_NOTIFICATIONS is a runtime permission from Android 13 on, and granted at install time
	/// before that - MAUI's check answers correctly for both, so there is no version test here.
	///
	/// On the main thread because Android requires it: a permission request has to be able to put an
	/// activity in front of the reader, and MAUI refuses the call outright anywhere else. Asking here
	/// rather than at the call site keeps the requirement with the platform that has it.
	/// </summary>
	private static Task<bool> IsPermittedAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
	{
		var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
		if (status == PermissionStatus.Granted)
		{
			return true;
		}

		// Android answers a second request from the earlier refusal without prompting again, so what
		// comes back is what the reader actually chose rather than a fresh chance to be asked.
		return await Permissions.RequestAsync<Permissions.PostNotifications>() == PermissionStatus.Granted;
	});
}
