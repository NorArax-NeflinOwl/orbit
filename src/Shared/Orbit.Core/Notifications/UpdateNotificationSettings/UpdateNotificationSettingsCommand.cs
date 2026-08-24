using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.UpdateNotificationSettings;

[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateNotificationSettingsCommand(
    Guid UserId, bool AllowNotifications, bool AllowPush, bool AllowEmail, bool AllowMobileBanner, bool ShowExceptionDetails)
    : IRequest<NotificationSettings>;
