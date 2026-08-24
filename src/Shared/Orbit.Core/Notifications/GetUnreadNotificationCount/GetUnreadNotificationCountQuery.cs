using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetUnreadNotificationCount;

public sealed record GetUnreadNotificationCountQuery(Guid UserId) : IRequest<int>;
