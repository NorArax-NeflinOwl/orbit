using Orbit.Core.Abstractions;

namespace Orbit.Core.PushNotifications.UnsubscribeFromPush;

[ClientAction(ClientActionCategory.PushNotificationToggle)]
public sealed record UnsubscribeFromPushCommand(Guid UserId, string Endpoint) : IRequest<bool>;
