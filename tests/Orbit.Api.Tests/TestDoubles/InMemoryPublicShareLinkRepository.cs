using Orbit.Core.Sharing;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="IPublicShareLinkRepository"/> stub for unit tests.</summary>
internal sealed class InMemoryPublicShareLinkRepository : IPublicShareLinkRepository
{
    private readonly List<PublicShareLink> _links = [];

    public Task AddAsync(PublicShareLink link, CancellationToken cancellationToken)
    {
        _links.Add(link);
        return Task.CompletedTask;
    }

    public Task<PublicShareLink?> GetByTokenAsync(string token, CancellationToken cancellationToken)
        => Task.FromResult(_links.FirstOrDefault(link => link.Token == token));

    public Task<PublicShareLink?> GetLiveForItemAsync(
        Guid ownerUserId, SharedItemType itemType, Guid itemId, CancellationToken cancellationToken)
        => Task.FromResult(_links.FirstOrDefault(link =>
            link.OwnerUserId == ownerUserId && link.ItemType == itemType && link.ItemId == itemId && !link.IsRevoked));

    /// <summary>The stub hands out the same instance it stores, so a revoke has already been applied by the time this runs.</summary>
    public Task UpdateAsync(PublicShareLink link, CancellationToken cancellationToken) => Task.CompletedTask;
}
