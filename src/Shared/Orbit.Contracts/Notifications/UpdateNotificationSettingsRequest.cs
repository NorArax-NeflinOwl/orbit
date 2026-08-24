namespace Orbit.Contracts.Notifications;

public sealed record UpdateNotificationSettingsRequest(
    bool AllowNotifications, bool AllowPush, bool AllowEmail, bool AllowMobileBanner, bool ShowExceptionDetails,
    int BannerVisibleSeconds, int BannerMinimumGapSeconds);
