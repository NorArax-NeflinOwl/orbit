namespace Orbit.Contracts.PushNotifications;

/// <summary>Registers (or refreshes) this browser's Web Push subscription with Orbit.Api.</summary>
public sealed record PushSubscriptionRequest(string Endpoint, string P256dhBase64, string AuthBase64);
