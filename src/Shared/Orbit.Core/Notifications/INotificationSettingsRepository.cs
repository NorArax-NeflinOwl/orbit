namespace Orbit.Core.Notifications;

/// <summary>One row per user, at most - see NotificationSettings' class comment for why it's created lazily rather than at registration.</summary>
public interface INotificationSettingsRepository
{
    /// <summary>Returns NotificationSettings.Default(userId) without inserting anything if no row exists yet.</summary>
    Task<NotificationSettings> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces the stored row for settings.UserId.</summary>
    Task UpsertAsync(NotificationSettings settings, CancellationToken cancellationToken);
}
