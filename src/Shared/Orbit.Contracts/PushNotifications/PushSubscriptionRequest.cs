namespace Orbit.Contracts.PushNotifications;

/// <summary>Registers (or refreshes) this browser's Web Push subscription with Orbit.Api.</summary>
public sealed record PushSubscriptionRequest(string Endpoint, string P256dhBase64, string AuthBase64);

/// <summary>
/// A mobile app registering for push. Platform is "Ios" or "Android" (see Orbit.Core.Mobile.MobilePlatform);
/// the server keeps it so a failure to deliver can be attributed to the right platform rather than guessed at.
/// </summary>
public sealed record DevicePushSubscriptionRequest(string DeviceToken, string Platform);
