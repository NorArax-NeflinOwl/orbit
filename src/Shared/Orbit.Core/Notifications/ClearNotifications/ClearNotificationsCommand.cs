using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.ClearNotifications;

/// <summary>Empties the notification feed - the panel's "Clear" action, which discards entries rather than marking them read.</summary>
public sealed record ClearNotificationsCommand(Guid UserId) : IRequest<bool>;
