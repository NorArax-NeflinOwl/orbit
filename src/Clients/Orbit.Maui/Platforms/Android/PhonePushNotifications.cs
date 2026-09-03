using Android.Gms.Extensions;
using Firebase;
using Firebase.Messaging;
using Orbit.Core.Mobile;
using Orbit.Mobile.Notifications;
using AndroidApplication = Android.App.Application;

namespace Orbit.Maui.Platform;

/// <summary>
/// Asks Android for permission to notify, and obtains the token Orbit sends to.
///
/// The same shape as the iOS implementation and for the same reasons, with one difference worth
/// knowing: Orbit's server delivers through Firebase Cloud Messaging, which reaches Android directly
/// rather than through a second service underneath. Android therefore needs no APNs auth key, which is
/// why this half works while the iOS one still answers NotAvailableHere - see info/orbit-maui-plan.md §4.2.
///
/// Nothing here displays anything. A message from Orbit carries a notification block, which Android
/// shows itself while the app is in the background, and its data carries the tap target that
/// MainActivity reads back out of the intent.
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

		var token = await ReadTokenAsync();
		return token is { Length: > 0 }
			? new PushRegistrationResult(PushRegistrationOutcome.Registered, token)
			: new PushRegistrationResult(PushRegistrationOutcome.NotAvailableHere);
	}

	/// <summary>
	/// The FCM registration token, or null when this build cannot have one.
	///
	/// A build made without google-services.json has no Firebase options to initialise from, and
	/// InitializeApp answers null rather than throwing - the one case where "no token" is about the
	/// build rather than the device, and the reason the result is separated from a refusal at all. See
	/// the GoogleServicesJson item in the csproj.
	///
	/// The token itself is minted by Play Services, so it is a network call in all but name: a device
	/// that has never reached Google has none yet, and the sign-in after it does gets one. That is why
	/// PushRegistration runs this on every sign-in rather than once.
	/// </summary>
	private static async Task<string?> ReadTokenAsync()
	{
		if (FirebaseApp.InitializeApp(AndroidApplication.Context) is null)
		{
			return null;
		}

		// GetToken hands back Play Services' own Task rather than a .NET one; AsAsync bridges the two.
		var token = await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.Object>();
		return token?.ToString();
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
