using Orbit.Core.Abstractions;

namespace Orbit.Core.Sharing.CreatePublicShareLink;

/// <summary>
/// Hands back the item's existing live link if it has one, and only mints a new one otherwise - see
/// IPublicShareLinkRepository.GetLiveForItemAsync for why one item must never have two live URLs.
/// Returns null when the caller doesn't own the item or it no longer exists, which the API turns into a
/// 404; a private item is refused with InvalidRequestException instead, since that is a "no, and here
/// is why" rather than a "there is nothing here".
/// </summary>
public sealed class CreatePublicShareLinkCommandHandler : IRequestHandler<CreatePublicShareLinkCommand, PublicShareLink?>
{
    private readonly IPublicShareLinkRepository _publicShareLinkRepository;
    private readonly PublicSharedItemReader _publicSharedItemReader;

    public CreatePublicShareLinkCommandHandler(
        IPublicShareLinkRepository publicShareLinkRepository, PublicSharedItemReader publicSharedItemReader)
    {
        _publicShareLinkRepository = publicShareLinkRepository;
        _publicSharedItemReader = publicSharedItemReader;
    }

    public async Task<PublicShareLink?> HandleAsync(CreatePublicShareLinkCommand request, CancellationToken cancellationToken)
    {
        if (!await _publicSharedItemReader.CanPublishAsync(request.OwnerUserId, request.ItemType, request.ItemId, cancellationToken))
        {
            return null;
        }

        var existingLink = await _publicShareLinkRepository.GetLiveForItemAsync(
            request.OwnerUserId, request.ItemType, request.ItemId, cancellationToken);
        if (existingLink is not null)
        {
            return existingLink;
        }

        var link = PublicShareLink.Create(request.OwnerUserId, request.ItemType, request.ItemId);
        await _publicShareLinkRepository.AddAsync(link, cancellationToken);
        return link;
    }
}
