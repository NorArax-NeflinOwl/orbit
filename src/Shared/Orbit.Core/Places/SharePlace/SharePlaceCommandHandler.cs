using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.Places.SharePlace;

/// <summary>
/// Hands a place to somebody - the caller's own, or one already handed to them - under the same three
/// rules the other four kinds follow (see ShareNoteCommandHandler, which states them at length):
/// the owner may share at any level, a recipient only within what their own grant permits, and nobody
/// may share a place back to the person who keeps it.
///
/// Nothing about a place is ever sealed, so the refusal a note carries for a private one has no
/// counterpart here - there is no place whose contents the server holds only as ciphertext.
/// </summary>
public sealed class SharePlaceCommandHandler : IRequestHandler<SharePlaceCommand, ShareOutcome?>
{
    private readonly PlaceAccessResolver _placeAccessResolver;
    private readonly IPlaceShareRepository _placeShareRepository;
    private readonly ISharedItemNotifier _sharedItemNotifier;

    public SharePlaceCommandHandler(
        PlaceAccessResolver placeAccessResolver,
        IPlaceShareRepository placeShareRepository,
        ISharedItemNotifier sharedItemNotifier)
    {
        _placeAccessResolver = placeAccessResolver;
        _placeShareRepository = placeShareRepository;
        _sharedItemNotifier = sharedItemNotifier;
    }

    public async Task<ShareOutcome?> HandleAsync(SharePlaceCommand request, CancellationToken cancellationToken)
    {
        var place = await _placeAccessResolver.ResolveAsync(request.OwnerUserId, request.PlaceId, cancellationToken);
        if (place is null || request.RecipientUserId == place.UserId)
        {
            return null;
        }

        if (place.IsShared && !place.AccessLevel.CanGrant(request.AccessLevel))
        {
            return null;
        }

        var existing = await _placeShareRepository.FindExistingAsync(place.Id, request.RecipientUserId, cancellationToken);
        if (existing is not null)
        {
            // Sharing again at a higher level raises what the standing offer gives rather than doing
            // nothing: that is how a request for edit access is answered.
            var raised = existing.RaiseAccessLevelTo(request.AccessLevel);
            if (raised)
            {
                await _placeShareRepository.UpdateAsync(existing, cancellationToken);
            }

            return new ShareOutcome(existing.Id, AlreadyShared: true, raised);
        }

        var share = PlaceShare.Create(place.Id, place.UserId, request.RecipientUserId, request.AccessLevel);
        await _placeShareRepository.AddAsync(share, cancellationToken);
        await _sharedItemNotifier.NotifyAsync(
            request.RecipientUserId, request.OwnerUserId, SharedItemKind.Place, place.Name,
            SharedItemLink.ToAccept(share.Id), cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
