using Orbit.Core.Mobile;

namespace Orbit.Core.Notifications;

/// <summary>
/// What a browser hands over when it subscribes: where to POST, and the two values RFC 8291's message
/// encryption needs. They are useless apart, so they travel as one value rather than three loose
/// strings threaded through every layer.
/// </summary>
public sealed record WebPushRegistration(string Endpoint, string P256dhBase64, string AuthBase64);

/// <summary>
/// What a mobile app hands over: the FCM registration token, and which app it came from. The platform
/// is kept because delivery differs beneath FCM - an iOS message only arrives if Firebase has an APNs
/// key for the app - so "why did this one fail" is answerable without guessing.
/// </summary>
public sealed record DeviceRegistration(string Token, MobilePlatform Platform);
