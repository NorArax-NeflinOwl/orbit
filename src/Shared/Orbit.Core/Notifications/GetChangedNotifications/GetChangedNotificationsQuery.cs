using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetChangedNotifications;

/// <summary>
/// What has changed about this user's notifications since a point in time - the delta a phone pulls so
/// it can keep its own copy and show the feed with no connection, the same way notes, task lists,
/// calendar events and warehouses already do.
/// </summary>
public sealed record GetChangedNotificationsQuery(Guid UserId, DateTimeOffset Since, int Take)
    : IRequest<IReadOnlyList<NotificationEntry>>;
