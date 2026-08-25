using Orbit.Core.Abstractions;

namespace Orbit.Core.Location.StopSharingLocation;

public sealed class StopSharingLocationCommandHandler : IRequestHandler<StopSharingLocationCommand, bool>
{
    private readonly ISharedLocationRepository _sharedLocationRepository;

    public StopSharingLocationCommandHandler(ISharedLocationRepository sharedLocationRepository)
    {
        _sharedLocationRepository = sharedLocationRepository;
    }

    /// <summary>Always true: stopping something that was never started is the state the caller asked for, not a failure.</summary>
    public async Task<bool> HandleAsync(StopSharingLocationCommand request, CancellationToken cancellationToken)
    {
        if (request.RecipientUserId is { } recipientUserId)
        {
            await _sharedLocationRepository.DeleteAsync(request.SharerUserId, recipientUserId, cancellationToken);
        }
        else
        {
            await _sharedLocationRepository.DeleteAllBySharerAsync(request.SharerUserId, cancellationToken);
        }

        return true;
    }
}
