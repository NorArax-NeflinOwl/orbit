using Orbit.Core.Abstractions;

namespace Orbit.Core.Tags.GetTagColours;

public sealed class GetTagColoursQueryHandler : IRequestHandler<GetTagColoursQuery, IReadOnlyList<TagColour>>
{
    private readonly ITagColourRepository _tagColourRepository;

    public GetTagColoursQueryHandler(ITagColourRepository tagColourRepository)
    {
        _tagColourRepository = tagColourRepository;
    }

    public Task<IReadOnlyList<TagColour>> HandleAsync(GetTagColoursQuery request, CancellationToken cancellationToken)
        => _tagColourRepository.GetAllAsync(request.UserId, cancellationToken);
}
