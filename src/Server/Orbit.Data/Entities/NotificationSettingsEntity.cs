namespace Orbit.Data.Entities;

/// <summary>One row per user, at most - see Orbit.Core.Notifications.INotificationSettingsRepository.</summary>
public sealed class NotificationSettingsEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public bool AllowNotifications { get; set; }
    public bool AllowPush { get; set; }
    public bool AllowEmail { get; set; }
    public bool AllowMobileBanner { get; set; }
    public bool ShowExceptionDetails { get; set; }
    public int BannerVisibleSeconds { get; set; }
    public int BannerMinimumGapSeconds { get; set; }
}
