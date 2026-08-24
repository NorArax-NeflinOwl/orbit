using Orbit.Core.Notifications;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="INotificationSettingsRepository"/> stub for unit tests.</summary>
internal sealed class InMemoryNotificationSettingsRepository : INotificationSettingsRepository
{
    private readonly Dictionary<Guid, NotificationSettings> _settingsByUserId = [];

    public Task<NotificationSettings> GetAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult(_settingsByUserId.TryGetValue(userId, out var settings) ? settings : NotificationSettings.Default(userId));

    public Task UpsertAsync(NotificationSettings settings, CancellationToken cancellationToken)
    {
        _settingsByUserId[settings.UserId] = settings;
        return Task.CompletedTask;
    }
}
