namespace Orbit.Contracts.Notifications;

public sealed record NotificationSettingsDto(
    bool AllowNotifications, bool AllowPush, bool AllowEmail, bool AllowMobileBanner, bool ShowExceptionDetails);
