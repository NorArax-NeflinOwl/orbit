using Orbit.Core.Abstractions;

namespace Orbit.Core.Sharing.RevokePublicShareLink;

[ClientAction(ClientActionCategory.Edit)]
public sealed record RevokePublicShareLinkCommand(Guid OwnerUserId, SharedItemType ItemType, Guid ItemId) : IRequest<bool>;
