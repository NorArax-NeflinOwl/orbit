namespace Orbit.Contracts.PushNotifications;

/// <summary>
/// The VAPID public key (see RFC 8292), base64url-encoded, a browser needs to create a Web Push
/// subscription via the Push API's <c>applicationServerKey</c>.
/// </summary>
public sealed record PushPublicKeyDto(string PublicKeyBase64Url);
