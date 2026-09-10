using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.AcceptPlaceShare;

public sealed class AcceptPlaceShareCommandHandler : IRequestHandler<AcceptPlaceShareCommand, bool>
{
    private readonly IPlaceShareRepository _placeShareRepository;

    public AcceptPlaceShareCommandHandler(IPlaceShareRepository placeShareRepository)
    {
        _placeShareRepository = placeShareRepository;
    }

    /// <summary>
    /// Marking the offer accepted is the whole of it: sharing grants access to the one row rather than
    /// making the recipient a copy - see PlaceAccessResolver, which reads the grant back on every load.
    /// </summary>
    public async Task<bool> HandleAsync(AcceptPlaceShareCommand request, CancellationToken cancellationToken)
    {
        var share = await _placeShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        if (share is null)
        {
            return false;
        }

        if (!share.IsAccepted)
        {
            share.MarkAccepted();
            await _placeShareRepository.UpdateAsync(share, cancellationToken);
        }

        return true;
    }
}
