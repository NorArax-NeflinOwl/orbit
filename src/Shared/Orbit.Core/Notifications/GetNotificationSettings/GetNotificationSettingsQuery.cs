using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetNotificationSettings;

public sealed record GetNotificationSettingsQuery(Guid UserId) : IRequest<NotificationSettings>;
