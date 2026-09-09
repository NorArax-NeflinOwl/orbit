using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.Sharing.RevokeShare;

/// <summary>
/// Takes back something the caller shared. The owner's side of the same act a recipient performs by
/// removing something from their own list: that drops their grant and leaves the owner's copy alone,
/// and this drops the same grant from the other end.
/// </summary>
[ClientAction(ClientActionCategory.ShareElement)]
public sealed record RevokeShareCommand(Guid OwnerUserId, SharedItemKind Kind, Guid ShareId) : IRequest<bool>;
