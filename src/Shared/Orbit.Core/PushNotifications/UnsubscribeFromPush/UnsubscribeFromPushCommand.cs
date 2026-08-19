using Orbit.Core.Abstractions;

namespace Orbit.Core.PushNotifications.UnsubscribeFromPush;

public sealed record UnsubscribeFromPushCommand(Guid UserId, string Endpoint) : IRequest<bool>;
