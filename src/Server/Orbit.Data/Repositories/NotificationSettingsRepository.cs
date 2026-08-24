using Microsoft.EntityFrameworkCore;
using Orbit.Core.Notifications;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class NotificationSettingsRepository : INotificationSettingsRepository
{
    private readonly OrbitDbContext _dbContext;

    public NotificationSettingsRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<NotificationSettings> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.NotificationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);

        return entity is null ? NotificationSettings.Default(userId) : ToDomain(entity);
    }

    public async Task UpsertAsync(NotificationSettings settings, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.NotificationSettings
            .FirstOrDefaultAsync(row => row.UserId == settings.UserId, cancellationToken);

        if (entity is null)
        {
            _dbContext.NotificationSettings.Add(ToEntity(settings));
        }
        else
        {
            entity.AllowNotifications = settings.AllowNotifications;
            entity.AllowPush = settings.AllowPush;
            entity.AllowEmail = settings.AllowEmail;
            entity.AllowMobileBanner = settings.AllowMobileBanner;
            entity.ShowExceptionDetails = settings.ShowExceptionDetails;
            entity.BannerVisibleSeconds = settings.BannerTiming.VisibleSeconds;
            entity.BannerMinimumGapSeconds = settings.BannerTiming.MinimumGapSeconds;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static NotificationSettings ToDomain(NotificationSettingsEntity entity)
        => NotificationSettings.FromPersistence(
            entity.UserId, entity.AllowNotifications, entity.AllowPush, entity.AllowEmail, entity.AllowMobileBanner, entity.ShowExceptionDetails,
            new BannerTiming(entity.BannerVisibleSeconds, entity.BannerMinimumGapSeconds));

    private static NotificationSettingsEntity ToEntity(NotificationSettings settings)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = settings.UserId,
            AllowNotifications = settings.AllowNotifications,
            AllowPush = settings.AllowPush,
            AllowEmail = settings.AllowEmail,
            AllowMobileBanner = settings.AllowMobileBanner,
            ShowExceptionDetails = settings.ShowExceptionDetails,
            BannerVisibleSeconds = settings.BannerTiming.VisibleSeconds,
            BannerMinimumGapSeconds = settings.BannerTiming.MinimumGapSeconds
        };
}
