using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetNotificationHistory;

/// <summary>
/// Everything still held for this user, including entries they have cleared out of the panel - what the
/// notifications page shows, as opposed to GetNotificationEntriesQuery's panel view.
/// </summary>
public sealed record GetNotificationHistoryQuery(Guid UserId, int Take) : IRequest<IReadOnlyList<NotificationEntry>>;
