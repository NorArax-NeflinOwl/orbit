using Orbit.Core.Abstractions;

namespace Orbit.Core.PushNotifications.SubscribeToPush;

public sealed record SubscribeToPushCommand(Guid UserId, string Endpoint, string P256dhBase64, string AuthBase64) : IRequest<bool>;
