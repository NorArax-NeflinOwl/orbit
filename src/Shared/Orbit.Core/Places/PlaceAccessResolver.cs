using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Places;

/// <summary>
/// Loads a place the way a given caller is actually allowed to see it - because they kept it, or because
/// somebody handed it to them - and stamps the result with that relationship. Mirrors NoteAccessResolver;
/// every read and write path goes through here rather than repeating the owner-or-grant lookup.
///
/// Shorter than the note's by exactly what a place does not have: nothing about a place is ever sealed,
/// so there is no privacy for a stale grant to outlive, and there is no per-recipient pin.
/// </summary>
public sealed class PlaceAccessResolver
{
    private readonly IPlaceRepository _placeRepository;
    private readonly IPlaceShareRepository _placeShareRepository;
    private readonly IUserRepository _userRepository;

    public PlaceAccessResolver(
        IPlaceRepository placeRepository, IPlaceShareRepository placeShareRepository, IUserRepository userRepository)
    {
        _placeRepository = placeRepository;
        _placeShareRepository = placeShareRepository;
        _userRepository = userRepository;
    }

    /// <summary>Null when the caller neither keeps this place nor holds an accepted share of it.</summary>
    public async Task<Place?> ResolveAsync(Guid callerId, Guid placeId, CancellationToken cancellationToken)
    {
        var kept = await _placeRepository.GetByIdAsync(callerId, placeId, cancellationToken);
        if (kept is not null)
        {
            var sharedOut = await _placeShareRepository.GetSharedOutPlaceIdsAsync(callerId, cancellationToken);
            kept.SetSharedWithOthers(sharedOut.Contains(placeId));
            return kept;
        }

        var grant = await _placeShareRepository.FindAcceptedGrantAsync(placeId, callerId, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        // The owner may have forgotten the place between granting access and this read. A dangling grant
        // reads as "not found" rather than throwing, the way every other stale reference here does.
        var place = await _placeRepository.GetByIdAsync(grant.OwnerUserId, grant.SourcePlaceId, cancellationToken);
        if (place is null)
        {
            return null;
        }

        var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
        place.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
        return place;
    }

    /// <summary>Every place the caller keeps, plus every place handed to them and taken up.</summary>
    public async Task<IReadOnlyList<Place>> ResolveAllAsync(
        Guid callerId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var kept = await _placeRepository.GetAllAsync(callerId, updatedSinceUtc, cancellationToken);

        // Asked once for the whole list rather than per place - see GetSharedOutPlaceIdsAsync.
        var sharedOut = await _placeShareRepository.GetSharedOutPlaceIdsAsync(callerId, cancellationToken);
        foreach (var place in kept)
        {
            place.SetSharedWithOthers(sharedOut.Contains(place.Id));
        }

        var granted = new List<Place>();
        foreach (var grant in await _placeShareRepository.GetAcceptedGrantsForRecipientAsync(callerId, cancellationToken))
        {
            var place = await _placeRepository.GetByIdAsync(grant.OwnerUserId, grant.SourcePlaceId, cancellationToken);
            if (place is null)
            {
                continue;
            }

            // A delta read asks for what changed since a moment; a place handed over must answer that
            // question the same way one kept does, or a phone would be told about the owner's edits and
            // never about the share itself.
            if (updatedSinceUtc is { } since && place.UpdatedAtUtc <= since && grant.CreatedAtUtc <= since)
            {
                continue;
            }

            var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
            place.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
            granted.Add(place);
        }

        return [.. kept, .. granted];
    }
}
