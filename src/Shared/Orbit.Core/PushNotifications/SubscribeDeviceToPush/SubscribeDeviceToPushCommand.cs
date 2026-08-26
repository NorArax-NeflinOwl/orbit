using Orbit.Core.Abstractions;
using Orbit.Core.Mobile;

namespace Orbit.Core.PushNotifications.SubscribeDeviceToPush;

/// <summary>
/// A mobile app registering for push. Separate from SubscribeToPushCommand because a phone has nothing
/// resembling a Web Push endpoint or its encryption keys - it has one token Firebase resolves, and
/// squeezing that into the browser's three fields would only make both shapes harder to read.
/// </summary>
[ClientAction(ClientActionCategory.PushNotificationToggle)]
public sealed record SubscribeDeviceToPushCommand(Guid UserId, string DeviceToken, MobilePlatform Platform) : IRequest<bool>;
