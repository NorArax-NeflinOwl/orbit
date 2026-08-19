using Microsoft.EntityFrameworkCore;
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
        var existing = await _dbContext.PushSubscriptions
            .FirstOrDefaultAsync(entity => entity.Endpoint == subscription.Endpoint, cancellationToken);

        if (existing is null)
        {
            _dbContext.PushSubscriptions.Add(ToEntity(subscription));
        }
        else
        {
            // The endpoint (its unique key - see OrbitDbContext) already exists, possibly for a
            // different user than before (e.g. someone else signed into the same browser and re-enabled
            // push) - every field is refreshed rather than only the keys, so the subscription always
            // belongs to whoever most recently subscribed with it.
            existing.UserId = subscription.UserId;
            existing.P256dhBase64 = subscription.P256dhBase64;
            existing.AuthBase64 = subscription.AuthBase64;
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
        => PushSubscription.FromPersistence(
            entity.Id, entity.UserId, entity.Endpoint, entity.P256dhBase64, entity.AuthBase64, entity.CreatedAtUtc);

    private static PushSubscriptionEntity ToEntity(PushSubscription subscription)
        => new()
        {
            Id = subscription.Id,
            UserId = subscription.UserId,
            Endpoint = subscription.Endpoint,
            P256dhBase64 = subscription.P256dhBase64,
            AuthBase64 = subscription.AuthBase64,
            CreatedAtUtc = subscription.CreatedAtUtc
        };
}
