using Orbit.Core.Abstractions;

namespace Orbit.Core.Location.StopReceivingLocation;

public sealed class StopReceivingLocationCommandHandler : IRequestHandler<StopReceivingLocationCommand, bool>
{
    private readonly ISharedLocationRepository _sharedLocationRepository;

    public StopReceivingLocationCommandHandler(ISharedLocationRepository sharedLocationRepository)
    {
        _sharedLocationRepository = sharedLocationRepository;
    }

    /// <summary>
    /// Always true, like stopping from the sharer's side: refusing something nobody is sharing is the
    /// state the caller asked for, not a failure. The pair is keyed the way the row is - the sharer
    /// first - so this deletes exactly the one row that was shared with this recipient.
    /// </summary>
    public async Task<bool> HandleAsync(StopReceivingLocationCommand request, CancellationToken cancellationToken)
    {
        await _sharedLocationRepository.DeleteAsync(request.SharerUserId, request.RecipientUserId, cancellationToken);
        return true;
    }
}
