using Orbit.Core.Places;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IPlaceShareRepository"/> for tests that need real add/lookup/update behaviour,
/// per-recipient scoping included, without a database. Mirrors InMemoryNoteShareRepository.
/// </summary>
internal sealed class InMemoryPlaceShareRepository : IPlaceShareRepository
{
    private readonly List<PlaceShare> _shares = [];

    public Task AddAsync(PlaceShare share, CancellationToken cancellationToken)
    {
        _shares.Add(share);
        return Task.CompletedTask;
    }

    public Task<PlaceShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(share => share.Id == id && share.RecipientUserId == recipientUserId));

    public Task<PlaceShare?> FindExistingAsync(Guid sourcePlaceId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(
            share => share.SourcePlaceId == sourcePlaceId && share.RecipientUserId == recipientUserId));

    public Task<PlaceShare?> FindAcceptedGrantAsync(Guid sourcePlaceId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(
            share => share.SourcePlaceId == sourcePlaceId && share.RecipientUserId == recipientUserId && share.IsAccepted));

    public Task<IReadOnlyList<PlaceShare>> GetAcceptedGrantsForRecipientAsync(
        Guid recipientUserId, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlaceShare> grants =
            [.. _shares.Where(share => share.RecipientUserId == recipientUserId && share.IsAccepted)];
        return Task.FromResult(grants);
    }

    public Task<IReadOnlySet<Guid>> GetSharedOutPlaceIdsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        IReadOnlySet<Guid> placeIds = _shares
            .Where(share => share.OwnerUserId == ownerUserId && share.IsAccepted)
            .Select(share => share.SourcePlaceId)
            .ToHashSet();

        return Task.FromResult(placeIds);
    }

    /// <summary>
    /// Nothing to replace: a handler mutates the same PlaceShare instance this already holds. Mirrors
    /// InMemoryNoteShareRepository, which says the same.
    /// </summary>
    public Task UpdateAsync(PlaceShare share, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveAcceptedGrantAsync(Guid sourcePlaceId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        _shares.RemoveAll(share =>
            share.SourcePlaceId == sourcePlaceId && share.RecipientUserId == recipientUserId && share.IsAccepted);
        return Task.CompletedTask;
    }

    /// <summary>Everything this owner handed that recipient, oldest first - the real one orders the same way.</summary>
    public Task<IReadOnlyList<PlaceShare>> GetSharesToAsync(
        Guid ownerUserId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlaceShare> shares =
        [
            .. _shares
                .Where(share => share.OwnerUserId == ownerUserId && share.RecipientUserId == recipientUserId)
                .OrderBy(share => share.CreatedAtUtc)
        ];

        return Task.FromResult(shares);
    }

    /// <summary>Scoped to the owner, exactly as the real one is: a share that is not theirs does not go.</summary>
    public Task<bool> RemoveAsync(Guid ownerUserId, Guid shareId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.RemoveAll(share => share.Id == shareId && share.OwnerUserId == ownerUserId) > 0);
}
