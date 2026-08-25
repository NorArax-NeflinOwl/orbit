using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.ShareWarehouse;

/// <summary>
/// OwnerUserId is really "the caller" - it doesn't have to be the warehouse's actual owner, just
/// someone with access to it (see ShareWarehouseCommandHandler). Returns null when the warehouse doesn't
/// exist, isn't accessible, can't be shared by this caller at the requested level, or the recipient is
/// the owner - the same "not found" either way. Mirrors ShareNoteCommand.
/// </summary>
[ClientAction(ClientActionCategory.ShareElement)]
public sealed record ShareWarehouseCommand(
    Guid OwnerUserId, Guid WarehouseId, Guid RecipientUserId, ShareAccessLevel AccessLevel = ShareAccessLevel.ReadOnly)
    : IRequest<ShareOutcome?>;
