namespace Orbit.Core.Sharing;

public interface IPublicShareLinkRepository
{
    Task AddAsync(PublicShareLink link, CancellationToken cancellationToken);

    /// <summary>The link behind a token, revoked ones included - the caller decides what a revoked link means.</summary>
    Task<PublicShareLink?> GetByTokenAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// The owner's existing live link for this item, if any. Asking for a link twice hands back the same
    /// one rather than minting a second: two live URLs for one item would both have to be revoked to
    /// take it back, and an owner who copied the first would have no idea the second existed.
    /// </summary>
    Task<PublicShareLink?> GetLiveForItemAsync(Guid ownerUserId, SharedItemType itemType, Guid itemId, CancellationToken cancellationToken);

    Task UpdateAsync(PublicShareLink link, CancellationToken cancellationToken);
}
