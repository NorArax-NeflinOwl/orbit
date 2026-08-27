using Orbit.Core.Mobile;
using Orbit.Mobile.Notifications;
using UserNotifications;

namespace Orbit.Maui.Platform;

/// <summary>
/// Asks iOS for permission to notify, and would obtain the token Orbit sends to.
///
/// The permission half is complete and worth having on its own: without it iOS shows nothing at all,
/// and the prompt has to have been answered long before a notification is ever sent.
///
/// The token half is not, and deliberately says so rather than inventing something. Orbit's server
/// delivers through Firebase Cloud Messaging, which reaches iOS through APNs underneath, so what it
/// needs is an FCM registration token - obtainable only with the Firebase SDK in the app, and useful
/// only once an APNs auth key is uploaded to the Firebase console. Registering a token that nothing can
/// deliver to would be worse than registering none: the server would count this device as reachable and
/// consider the notification sent. See info/orbit-maui-plan.md §4.2.1, which is about exactly this
/// failure being undetectable from the server.
///
/// What replaces this: obtain the FCM token and return it with
/// <see cref="PushRegistrationOutcome.Registered"/>. Nothing above this class changes - PushRegistration
/// already sends whatever comes back, and the tap handling below already works.
/// </summary>
public sealed class PhonePushNotifications : IDevicePushNotifications
{
	/// <summary>What Orbit is asking iOS to be allowed to do. No badge: nothing here maintains a count.</summary>
	private const UNAuthorizationOptions RequestedOptions = UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound;

	public string Platform => nameof(MobilePlatform.Ios);

	public async Task<PushRegistrationResult> RegisterAsync(CancellationToken cancellationToken = default)
	{
		if (!await IsPermittedAsync())
		{
			return new PushRegistrationResult(PushRegistrationOutcome.NotPermitted);
		}

		// Permission is granted and taps already arrive (see NotificationTapListener) - only the token
		// that would let the server start a notification is missing.
		return new PushRegistrationResult(PushRegistrationOutcome.NotAvailableHere);
	}

	/// <summary>
	/// Asks only when iOS has not been asked before. Requesting again after a refusal does not re-prompt
	/// - iOS answers from the earlier decision - so this reports what the reader actually chose rather
	/// than pretending a second ask might succeed.
	/// </summary>
	private static async Task<bool> IsPermittedAsync()
	{
		var centre = UNUserNotificationCenter.Current;
		var settings = await centre.GetNotificationSettingsAsync();

		if (settings.AuthorizationStatus == UNAuthorizationStatus.NotDetermined)
		{
			var (granted, _) = await centre.RequestAuthorizationAsync(RequestedOptions);
			return granted;
		}

		return settings.AuthorizationStatus is UNAuthorizationStatus.Authorized or UNAuthorizationStatus.Provisional;
	}
}
