using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.ShareInventory;

/// <summary>
/// OwnerUserId is really "the caller" - it doesn't have to be the inventory's actual owner, just
/// someone with access to it (see ShareInventoryCommandHandler). Returns null when the inventory doesn't
/// exist, isn't accessible, can't be shared by this caller at the requested level, or the recipient is
/// the owner - the same "not found" either way. Mirrors ShareNoteCommand.
/// </summary>
[ClientAction(ClientActionCategory.ShareElement)]
public sealed record ShareInventoryCommand(
    Guid OwnerUserId, Guid InventoryId, Guid RecipientUserId, ShareAccessLevel AccessLevel = ShareAccessLevel.ReadOnly)
    : IRequest<ShareOutcome?>;
