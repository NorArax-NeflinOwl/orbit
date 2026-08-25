using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.ClearNotifications;

/// <summary>
/// The panel's "Clear" action: clears the feed out of the way without destroying it. Entries stay
/// readable on the notifications page until the reader's retention window deletes them - see
/// NotificationEntry.Dismiss.
/// </summary>
public sealed record ClearNotificationsCommand(Guid UserId) : IRequest<bool>;
