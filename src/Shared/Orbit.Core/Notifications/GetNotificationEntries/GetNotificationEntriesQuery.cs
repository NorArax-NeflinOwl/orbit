using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetNotificationEntries;

public sealed record GetNotificationEntriesQuery(Guid UserId, int Take) : IRequest<IReadOnlyList<NotificationEntry>>;
