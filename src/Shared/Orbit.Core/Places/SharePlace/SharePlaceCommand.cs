using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.SharePlace;

/// <summary>
/// OwnerUserId is really "the caller": it does not have to be the place's actual owner, only somebody
/// with access to it. Null when the place does not exist or is not theirs to see, when they may not
/// share it at the level asked for, or when the recipient is the place's own owner - the same answer
/// either way, so a caller cannot tell "not there" from "not yours" by probing ids.
/// </summary>
[ClientAction(ClientActionCategory.ShareElement)]
public sealed record SharePlaceCommand(
    Guid OwnerUserId, Guid PlaceId, Guid RecipientUserId, ShareAccessLevel AccessLevel = ShareAccessLevel.ReadOnly)
    : IRequest<ShareOutcome?>;
