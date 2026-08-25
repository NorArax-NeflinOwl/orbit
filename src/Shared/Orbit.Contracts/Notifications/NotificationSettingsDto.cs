namespace Orbit.Contracts.Notifications;

public sealed record NotificationSettingsDto(
    bool AllowNotifications, bool AllowPush, bool AllowEmail, bool AllowMobileBanner, bool ShowExceptionDetails,
    int BannerVisibleSeconds, int BannerMinimumGapSeconds, bool AllowShareNotifications = false);
