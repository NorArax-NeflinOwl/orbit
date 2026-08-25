using Orbit.Core.Abstractions;

namespace Orbit.Core.Location.GetSharedLocations;

public sealed class GetSharedLocationsQueryHandler : IRequestHandler<GetSharedLocationsQuery, IReadOnlyList<SharedLocation>>
{
    private readonly ISharedLocationRepository _sharedLocationRepository;

    public GetSharedLocationsQueryHandler(ISharedLocationRepository sharedLocationRepository)
    {
        _sharedLocationRepository = sharedLocationRepository;
    }

    public Task<IReadOnlyList<SharedLocation>> HandleAsync(GetSharedLocationsQuery request, CancellationToken cancellationToken)
        => _sharedLocationRepository.GetSharedWithAsync(request.UserId, cancellationToken);
}

public sealed class GetOwnLocationSharesQueryHandler : IRequestHandler<GetOwnLocationSharesQuery, IReadOnlyList<SharedLocation>>
{
    private readonly ISharedLocationRepository _sharedLocationRepository;

    public GetOwnLocationSharesQueryHandler(ISharedLocationRepository sharedLocationRepository)
    {
        _sharedLocationRepository = sharedLocationRepository;
    }

    public Task<IReadOnlyList<SharedLocation>> HandleAsync(GetOwnLocationSharesQuery request, CancellationToken cancellationToken)
        => _sharedLocationRepository.GetSharedByAsync(request.UserId, cancellationToken);
}
