using Orbit.Core.Abstractions;

namespace Orbit.Core.Location.StopReceivingLocation;

/// <summary>
/// Ends a share from the other end: the recipient says they no longer want this person's position.
///
/// The same row as <see cref="StopSharingLocation.StopSharingLocationCommand"/> deletes, reached from
/// the other side of it. A share is an arrangement between two people, and only one of them could
/// previously end it - which left a reader with somebody's live position on their map and nothing to do
/// about it but ask them to stop.
///
/// Nothing stops the sharer starting again; this is not a block, it is a refusal of what is there now.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record StopReceivingLocationCommand(Guid RecipientUserId, Guid SharerUserId) : IRequest<bool>;
