namespace Orbit.Core.Places;

/// <summary>Mirrors INoteShareRepository - see PlaceShare on why a place's grants are the simpler half of it.</summary>
public interface IPlaceShareRepository
{
    Task AddAsync(PlaceShare share, CancellationToken cancellationToken);

    /// <summary>
    /// Scoped to the recipient, so a share offered to somebody else reads exactly as one that never
    /// existed - a caller cannot tell the two apart by probing ids.
    /// </summary>
    Task<PlaceShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(PlaceShare share, CancellationToken cancellationToken);

    /// <summary>The offer already made for this place to this recipient, accepted or still pending.</summary>
    Task<PlaceShare?> FindExistingAsync(Guid sourcePlaceId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>The accepted grant only - what PlaceAccessResolver treats as "they have access right now".</summary>
    Task<PlaceShare?> FindAcceptedGrantAsync(Guid sourcePlaceId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Every place this recipient has taken up, whoever handed it to them.</summary>
    Task<IReadOnlyList<PlaceShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Which of this owner's own places somebody else currently holds access to. A whole set in one
    /// query rather than a question per place, because the caller asks it of every place in a list.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetSharedOutPlaceIdsAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>Drops the recipient's own access, leaving the owner's place alone. A no-op when there is none.</summary>
    Task RemoveAcceptedGrantAsync(Guid sourcePlaceId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Everything this owner has handed to this person, offered or taken up - what a contact's page lists.</summary>
    Task<IReadOnlyList<PlaceShare>> GetSharesToAsync(Guid ownerUserId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Withdraws one share the owner made. Says whether a row actually went.</summary>
    Task<bool> RemoveAsync(Guid ownerUserId, Guid shareId, CancellationToken cancellationToken);
}
