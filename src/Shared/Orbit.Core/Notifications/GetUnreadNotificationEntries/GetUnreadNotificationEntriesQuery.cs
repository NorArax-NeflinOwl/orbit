using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetUnreadNotificationEntries;

public sealed record GetUnreadNotificationEntriesQuery(Guid UserId, int Take) : IRequest<IReadOnlyList<NotificationEntry>>;
