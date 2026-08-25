using Orbit.Core.Abstractions;

namespace Orbit.Core.Sharing.RevokePublicShareLink;

public sealed class RevokePublicShareLinkCommandHandler : IRequestHandler<RevokePublicShareLinkCommand, bool>
{
    private readonly IPublicShareLinkRepository _publicShareLinkRepository;

    public RevokePublicShareLinkCommandHandler(IPublicShareLinkRepository publicShareLinkRepository)
    {
        _publicShareLinkRepository = publicShareLinkRepository;
    }

    /// <summary>
    /// True even when there was no link to revoke: the end state asked for is "nobody can reach this
    /// through a link", which is already the case - the same reading StopSharingLocation takes.
    /// </summary>
    public async Task<bool> HandleAsync(RevokePublicShareLinkCommand request, CancellationToken cancellationToken)
    {
        var link = await _publicShareLinkRepository.GetLiveForItemAsync(
            request.OwnerUserId, request.ItemType, request.ItemId, cancellationToken);
        if (link is null)
        {
            return true;
        }

        link.Revoke();
        await _publicShareLinkRepository.UpdateAsync(link, cancellationToken);
        return true;
    }
}
