using Orbit.Core.Abstractions;

namespace Orbit.Core.Sharing.GetPublicSharedItem;

public sealed class GetPublicSharedItemQueryHandler : IRequestHandler<GetPublicSharedItemQuery, PublicSharedItem?>
{
    private readonly IPublicShareLinkRepository _publicShareLinkRepository;
    private readonly PublicSharedItemReader _publicSharedItemReader;

    public GetPublicSharedItemQueryHandler(
        IPublicShareLinkRepository publicShareLinkRepository, PublicSharedItemReader publicSharedItemReader)
    {
        _publicShareLinkRepository = publicShareLinkRepository;
        _publicSharedItemReader = publicSharedItemReader;
    }

    /// <summary>
    /// Null covers every way a link can fail to show something - unknown token, revoked link, item
    /// deleted, item since made private - and they all read the same to a caller on purpose. Saying
    /// which would tell someone guessing tokens that they had guessed one.
    /// </summary>
    public async Task<PublicSharedItem?> HandleAsync(GetPublicSharedItemQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return null;
        }

        var link = await _publicShareLinkRepository.GetByTokenAsync(request.Token, cancellationToken);
        if (link is null || link.IsRevoked)
        {
            return null;
        }

        return await _publicSharedItemReader.ReadAsync(link, cancellationToken);
    }
}
