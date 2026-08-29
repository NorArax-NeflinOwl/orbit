using Microsoft.EntityFrameworkCore;
using Orbit.Core.Mobile;
using Orbit.Core.Notifications;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class PushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly OrbitDbContext _dbContext;

    public PushSubscriptionRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PushSubscription>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.PushSubscriptions
            .AsNoTracking()
            .Where(entity => entity.UserId == userId)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task AddOrReplaceAsync(PushSubscription subscription, CancellationToken cancellationToken)
    {
        // Matched on whichever value identifies this destination: a browser keeps its endpoint across
        // re-subscribes, an app keeps its registration token, and both are unique (see OrbitDbContext).
        var endpoint = subscription.WebPush?.Endpoint;
        var deviceToken = subscription.Device?.Token;
        var existing = await _dbContext.PushSubscriptions.FirstOrDefaultAsync(
            entity => (endpoint != null && entity.Endpoint == endpoint)
                || (deviceToken != null && entity.DeviceToken == deviceToken),
            cancellationToken);

        if (existing is null)
        {
            _dbContext.PushSubscriptions.Add(ToEntity(subscription));
        }
        else
        {
            // The destination already exists, possibly for a different user than before (someone else
            // signed into the same browser, or the same phone) - every field is refreshed rather than
            // only the keys, so it always belongs to whoever most recently subscribed with it.
            existing.UserId = subscription.UserId;
            existing.Transport = subscription.Transport.ToString();
            existing.P256dhBase64 = subscription.WebPush?.P256dhBase64;
            existing.AuthBase64 = subscription.WebPush?.AuthBase64;
            existing.DevicePlatform = subscription.Device?.Platform.ToString();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveByEndpointAsync(Guid userId, string endpoint, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PushSubscriptions
            .FirstOrDefaultAsync(e => e.UserId == userId && e.Endpoint == endpoint, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _dbContext.PushSubscriptions.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RemoveAsync(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PushSubscriptions.FirstOrDefaultAsync(e => e.Id == subscriptionId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbContext.PushSubscriptions.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static PushSubscription ToDomain(PushSubscriptionEntity entity)
    {
        var transport = Enum.TryParse<PushTransport>(entity.Transport, out var parsed) ? parsed : PushTransport.WebPush;
        return PushSubscription.FromPersistence(
            entity.Id, entity.UserId, transport, ToWebPush(entity), ToDevice(entity), entity.CreatedAtUtc);
    }

    /// <summary>Null unless all three Web Push columns are present - they are only ever written together.</summary>
    private static WebPushRegistration? ToWebPush(PushSubscriptionEntity entity)
        => entity.Endpoint is not null && entity.P256dhBase64 is not null && entity.AuthBase64 is not null
            ? new WebPushRegistration(entity.Endpoint, entity.P256dhBase64, entity.AuthBase64)
            : null;

    private static DeviceRegistration? ToDevice(PushSubscriptionEntity entity)
        => entity.DeviceToken is not null && Enum.TryParse<MobilePlatform>(entity.DevicePlatform, out var platform)
            ? new DeviceRegistration(entity.DeviceToken, platform)
            : null;

    private static PushSubscriptionEntity ToEntity(PushSubscription subscription)
        => new()
        {
            Id = subscription.Id,
            UserId = subscription.UserId,
            Transport = subscription.Transport.ToString(),
            Endpoint = subscription.WebPush?.Endpoint,
            P256dhBase64 = subscription.WebPush?.P256dhBase64,
            AuthBase64 = subscription.WebPush?.AuthBase64,
            DeviceToken = subscription.Device?.Token,
            DevicePlatform = subscription.Device?.Platform.ToString(),
            CreatedAtUtc = subscription.CreatedAtUtc
        };
}
