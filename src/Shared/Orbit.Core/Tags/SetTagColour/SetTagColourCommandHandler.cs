using Orbit.Core.Abstractions;

namespace Orbit.Core.Tags.SetTagColour;

public sealed class SetTagColourCommandHandler : IRequestHandler<SetTagColourCommand, IReadOnlyList<TagColour>>
{
    private readonly ITagColourRepository _tagColourRepository;

    public SetTagColourCommandHandler(ITagColourRepository tagColourRepository)
    {
        _tagColourRepository = tagColourRepository;
    }

    /// <summary>
    /// Refused rather than tidied: a tag with no name has nothing to colour, and a colour that is not
    /// "#rrggbb" is one no client would know how to draw - both are a client's mistake worth hearing about.
    /// </summary>
    public async Task<IReadOnlyList<TagColour>> HandleAsync(SetTagColourCommand request, CancellationToken cancellationToken)
    {
        var tag = request.Tag.Trim();
        if (tag.Length == 0)
        {
            throw new InvalidRequestException("A tag needs a name to have a colour.");
        }

        StoredTextLimits.OrRefuse(tag, StoredTextLimits.Category, "tag");

        var colour = request.Colour.Trim();
        if (colour.Length == 0)
        {
            await _tagColourRepository.RemoveAsync(request.UserId, tag, cancellationToken);
        }
        else if (TagColour.IsAColour(colour))
        {
            await _tagColourRepository.SetAsync(request.UserId, tag, colour.ToLowerInvariant(), cancellationToken);
        }
        else
        {
            throw new InvalidRequestException("A tag's colour is written as #rrggbb.");
        }

        return await _tagColourRepository.GetAllAsync(request.UserId, cancellationToken);
    }
}
