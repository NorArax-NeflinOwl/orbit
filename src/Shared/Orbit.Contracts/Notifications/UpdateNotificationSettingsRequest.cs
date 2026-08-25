namespace Orbit.Contracts.Notifications;

/// <summary>
/// AllowShareNotifications adds push and email when something is shared with this user; the entry in
/// their notification feed happens either way - see Orbit.Core.Notifications.NotificationEntryKind.SharedWithYou.
/// Defaulted so an older client that doesn't send it leaves the switch off rather than silently turning
/// it on.
/// </summary>
public sealed record UpdateNotificationSettingsRequest(
    bool AllowNotifications, bool AllowPush, bool AllowEmail, bool AllowMobileBanner, bool ShowExceptionDetails,
    int BannerVisibleSeconds, int BannerMinimumGapSeconds, bool AllowShareNotifications = false,
    int RetentionDays = 3);
