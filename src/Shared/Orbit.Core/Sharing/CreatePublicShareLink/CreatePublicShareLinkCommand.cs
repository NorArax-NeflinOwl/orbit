using Orbit.Core.Abstractions;

namespace Orbit.Core.Sharing.CreatePublicShareLink;

[ClientAction(ClientActionCategory.Edit)]
public sealed record CreatePublicShareLinkCommand(Guid OwnerUserId, SharedItemType ItemType, Guid ItemId)
    : IRequest<PublicShareLink?>;
