using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.GetPlaceShareStatus;

public sealed class GetPlaceShareStatusQueryHandler : IRequestHandler<GetPlaceShareStatusQuery, bool?>
{
    private readonly IPlaceShareRepository _placeShareRepository;

    public GetPlaceShareStatusQueryHandler(IPlaceShareRepository placeShareRepository)
    {
        _placeShareRepository = placeShareRepository;
    }

    public async Task<bool?> HandleAsync(GetPlaceShareStatusQuery request, CancellationToken cancellationToken)
    {
        var share = await _placeShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        return share?.IsAccepted;
    }
}
